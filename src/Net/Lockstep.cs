using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Godot;

namespace Mandible;

/// Two-player deterministic lockstep over ENet (UDP; desktop, Android and iOS).
/// Only orders travel the network. The simulation advances in turns of 3 ticks (100 ms);
/// orders given now execute `Delay` turns later on both machines at the same tick.
public partial class Lockstep : Node, IOrders
{
    public const int Port = 47010, DiscoveryPort = 47011;
    public const int TicksPerTurn = 3;
    const string Version = "mandible-mp-6";
    const int CheckEvery = 10; // turns between desync checks

    public enum Phase { Idle, Hosting, Connecting, Connected, InGame, Closed }
    public Phase State { get; private set; } = Phase.Idle;
    public bool IsHost { get; private set; }
    /// The host picks a race (colony); the guest leads the other one.
    public int HostTeam;
    public GameMode Mode;                       // chosen by the host
    public AntType MyHero = AntType.Warlord, PeerHero = AntType.Warlord; // Lane War heroes
    public int Me => IsHost ? HostTeam : 1 - HostTeam;
    public string MyName = "Player", PeerName = "";
    public int Delay = 2;
    public uint Seed;
    public float PingMs = -1;
    public string Status = "";
    public string LanAddress = "", InternetAddress = "";
    public int Desyncs, ChecksOk;
    public event Action OnConnected, OnDisconnected, OnStart;

    ENetMultiplayerPeer _peer;
    long _remote;
    readonly Dictionary<int, List<Cmd>>[] _turns = { new(), new() };
    readonly List<Cmd> _pending = new();
    readonly Dictionary<int, uint> _mine = new(), _theirs = new();
    double _lastPing, _clock;
    PacketPeerUdp _beacon;
    float _beaconT;
    System.Threading.Thread _upnpThread;
    Upnp _upnp;

    // ------------------------------------------------------------------ connection
    public Error Host(string name, bool internet)
    {
        MyName = name;
        IsHost = true;
        _peer = new ENetMultiplayerPeer();
        var err = _peer.CreateServer(Port, 1);
        if (err != Error.Ok) { Status = $"Could not open port {Port} ({err})."; return err; }
        Hook();
        State = Phase.Hosting;
        LanAddress = LocalAddress();
        Status = $"Waiting for an opponent.  LAN address: {LanAddress}";
        _beacon = new PacketPeerUdp();
        _beacon.SetBroadcastEnabled(true);
        _beacon.SetDestAddress("255.255.255.255", DiscoveryPort);
        if (internet) OpenRouterPort();
        return Error.Ok;
    }

    public Error Join(string address, string name)
    {
        MyName = name;
        IsHost = false;
        int port = Port;
        address = address.Trim();
        int colon = address.LastIndexOf(':');
        if (colon > 0 && int.TryParse(address[(colon + 1)..], out var p)) { port = p; address = address[..colon]; }
        _peer = new ENetMultiplayerPeer();
        var err = _peer.CreateClient(address, port);
        if (err != Error.Ok) { Status = $"Could not reach {address} ({err})."; return err; }
        Hook();
        State = Phase.Connecting;
        Status = $"Connecting to {address}:{port}...";
        return Error.Ok;
    }

    void Hook()
    {
        _peer.PeerConnected += id =>
        {
            _remote = id;
            Send(Packet(1, w => { w.Write(Version); w.Write(MyName); w.Write((byte)MyHero); }));
        };
        _peer.PeerDisconnected += _ =>
        {
            var was = State;
            State = Phase.Closed;
            Status = $"{(PeerName == "" ? "The opponent" : PeerName)} disconnected.";
            if (was != Phase.Closed) OnDisconnected?.Invoke();
        };
    }

    /// Host only: lock in the match. Input delay adapts to the measured ping.
    public void StartMatch()
    {
        if (!IsHost || State != Phase.Connected) return;
        float ping = PingMs < 0 ? 60 : PingMs;
        Delay = Math.Clamp((int)Math.Ceiling((ping * 0.5f + 60f) / (TicksPerTurn * 1000f / 30f)) + 1, 2, 6);
        Seed = (uint)GD.Randi();
        Send(Packet(2, w => { w.Write(Seed); w.Write(Delay); w.Write((byte)HostTeam); w.Write((byte)Mode); }));
        Begin();
    }

    void Begin()
    {
        for (int t = 0; t < Delay; t++) { _turns[0][t] = new List<Cmd>(); _turns[1][t] = new List<Cmd>(); }
        State = Phase.InGame;
        Status = "";
        OnStart?.Invoke();
    }

    public void Leave()
    {
        if (_peer != null && State != Phase.Closed && State != Phase.Idle)
        {
            Send(Packet(6, _ => { }));
            _peer.Poll();
            _peer.Close();
        }
        if (_upnp != null) { try { _upnp.DeletePortMapping(Port, "UDP"); } catch { } }
        State = Phase.Closed;
    }

    // ------------------------------------------------------------------ lockstep
    public void Issue(Cmd c) => _pending.Add(c);

    public bool Ready(int turn) => _turns[0].ContainsKey(turn) && _turns[1].ContainsKey(turn);

    /// Applies both colonies' orders for `turn` (colony 0 first, always), then ships our
    /// orders for turn + Delay with a checksum of the state we executed from.
    public void Execute(int turn, Sim sim)
    {
        bool check = turn % CheckEvery == 0;
        uint sum = check ? sim.Checksum() : 0;
        if (check) { _mine[turn] = sum; Compare(turn); }
        for (int team = 0; team < 2; team++)
        {
            foreach (var c in _turns[team][turn]) if (c.Team == team) c.Apply(sim);
            _turns[team].Remove(turn);
        }
        int target = turn + Delay;
        var mine = new List<Cmd>(_pending);
        _pending.Clear();
        _turns[Me][target] = mine;
        Send(Packet(3, w =>
        {
            w.Write(target);
            w.Write(check ? turn : -1);
            w.Write(sum);
            w.Write((ushort)mine.Count);
            foreach (var c in mine) c.Write(w);
        }));
    }

    void Compare(int turn)
    {
        if (!_mine.TryGetValue(turn, out var a) || !_theirs.TryGetValue(turn, out var b)) return;
        if (a == b) ChecksOk++;
        else
        {
            Desyncs++;
            GD.PrintErr($"[net] DESYNC at turn {turn}: {a:X8} vs {b:X8}");
        }
        _mine.Remove(turn);
        _theirs.Remove(turn);
    }

    // ------------------------------------------------------------------ transport
    static byte[] Packet(byte type, Action<BinaryWriter> body)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write(type);
        body(w);
        w.Flush();
        return ms.ToArray();
    }

    // Test-only network weather: MANDIBLE_NETLAG=ms delays every outgoing packet by ms ± ms/2 of jitter
    // (order is kept, as on a real reliable channel). Used to prove the lockstep copes with mobile links.
    static readonly int LagMs = int.TryParse(System.Environment.GetEnvironmentVariable("MANDIBLE_NETLAG"), out var lag) ? lag : 0;
    readonly Queue<(double at, byte[] data)> _outbox = new();
    readonly Random _jitter = new(7);
    double _lastOut;

    void Send(byte[] data)
    {
        if (_peer == null || _remote == 0 || State == Phase.Closed) return;
        if (LagMs > 0)
        {
            _lastOut = Math.Max(_lastOut, _clock + (LagMs + (_jitter.NextDouble() - 0.5) * LagMs) / 1000.0);
            _outbox.Enqueue((_lastOut, data));
            return;
        }
        SendNow(data);
    }

    void SendNow(byte[] data)
    {
        _peer.TransferMode = MultiplayerPeer.TransferModeEnum.Reliable;
        _peer.SetTargetPeer((int)_remote);
        _peer.PutPacket(data);
    }

    public override void _Process(double delta)
    {
        _clock += delta;
        if (_peer == null || State is Phase.Idle or Phase.Closed) return;
        while (_outbox.Count > 0 && _outbox.Peek().at <= _clock) SendNow(_outbox.Dequeue().data);
        _peer.Poll();
        while (_peer.GetAvailablePacketCount() > 0)
        {
            var data = _peer.GetPacket();
            try { Receive(data); }
            catch (Exception e) { GD.PrintErr($"[net] bad packet: {e.Message}"); }
        }
        if (State is Phase.Connected or Phase.InGame && _clock - _lastPing > 1.0)
        {
            _lastPing = _clock;
            Send(Packet(4, w => w.Write(_clock)));
        }
        if (State == Phase.Hosting && _beacon != null)
        {
            _beaconT -= (float)delta;
            if (_beaconT <= 0)
            {
                _beaconT = 1f;
                _beacon.PutPacket(Encoding.UTF8.GetBytes($"MANDIBLE|{Version}|{MyName}|{Port}"));
            }
        }
        if (_upnpThread != null && !_upnpThread.IsAlive)
        {
            _upnpThread = null;
            Status = InternetAddress != ""
                ? $"Waiting for an opponent.  Internet: {InternetAddress}:{Port}   LAN: {LanAddress}"
                : $"Router did not open the port automatically (UPnP). Forward UDP {Port} to {LanAddress}, or play on LAN.";
        }
    }

    void Receive(byte[] data)
    {
        using var r = new BinaryReader(new MemoryStream(data));
        switch (r.ReadByte())
        {
            case 1: // hello
            {
                string ver = r.ReadString();
                if (ver != Version) { Status = $"Version mismatch ({ver} vs {Version}). Both players need the same Mandible release."; Leave(); return; }
                PeerName = r.ReadString();
                PeerHero = (AntType)r.ReadByte();
                if (State is Phase.Hosting or Phase.Connecting)
                {
                    State = Phase.Connected;
                    Status = IsHost ? $"{PeerName} joined. Press Start." : $"Connected to {PeerName}. Waiting for the host to start.";
                    OnConnected?.Invoke();
                }
                break;
            }
            case 2: // start
                Seed = r.ReadUInt32();
                Delay = r.ReadInt32();
                HostTeam = r.ReadByte();
                Mode = (GameMode)r.ReadByte();
                Begin();
                break;
            case 3: // orders for a turn
            {
                int turn = r.ReadInt32();
                int checkTurn = r.ReadInt32();
                uint sum = r.ReadUInt32();
                int n = r.ReadUInt16();
                var list = new List<Cmd>(n);
                for (int i = 0; i < n; i++) list.Add(Cmd.Read(r));
                _turns[1 - Me][turn] = list;
                if (checkTurn >= 0) { _theirs[checkTurn] = sum; Compare(checkTurn); }
                break;
            }
            case 4: // ping
            {
                double t = r.ReadDouble();
                Send(Packet(5, w => w.Write(t)));
                break;
            }
            case 5: // pong
            {
                double t = r.ReadDouble();
                float ms = (float)((_clock - t) * 1000.0);
                PingMs = PingMs < 0 ? ms : Mathf.Lerp(PingMs, ms, 0.3f);
                break;
            }
            case 6: // bye
                State = Phase.Closed;
                Status = $"{PeerName} left the game.";
                OnDisconnected?.Invoke();
                break;
        }
    }

    // ------------------------------------------------------------------ addresses
    public static string LocalAddress()
    {
        foreach (var a in IP.GetLocalAddresses())
            if (a.StartsWith("192.168.") || a.StartsWith("10.") || (a.StartsWith("172.") && int.TryParse(a.Split('.')[1], out var b) && b >= 16 && b <= 31))
                return a;
        return "127.0.0.1";
    }

    /// Asks the router to forward our UDP port (UPnP) so friends can join over the internet.
    void OpenRouterPort()
    {
        Status = $"Opening the router port for internet play...  LAN address: {LanAddress}";
        _upnpThread = new System.Threading.Thread(() =>
        {
            try
            {
                var u = new Upnp();
                if (u.Discover(2000, 2, "InternetGatewayDevice") == (int)Upnp.UpnpResult.Success && u.GetGateway() != null && u.GetGateway().IsValidGateway())
                {
                    if (u.AddPortMapping(Port, Port, "Mandible", "UDP", 0) == (int)Upnp.UpnpResult.Success)
                    {
                        InternetAddress = u.QueryExternalAddress();
                        _upnp = u;
                    }
                }
            }
            catch (Exception e) { GD.PrintErr($"[net] upnp: {e.Message}"); }
        });
        _upnpThread.Start();
    }
}

/// Listens for hosts announcing themselves on the local network.
public partial class LanBrowser : Node
{
    public readonly Dictionary<string, (string name, double seen)> Hosts = new();
    PacketPeerUdp _udp;
    double _clock;

    public override void _Ready()
    {
        _udp = new PacketPeerUdp();
        var err = _udp.Bind(Lockstep.DiscoveryPort);
        if (err != Error.Ok) GD.PrintErr($"[net] LAN browser could not listen ({err})");
    }

    public override void _Process(double delta)
    {
        _clock += delta;
        while (_udp != null && _udp.GetAvailablePacketCount() > 0)
        {
            var text = Encoding.UTF8.GetString(_udp.GetPacket());
            string ip = _udp.GetPacketIP();
            var parts = text.Split('|');
            if (parts.Length >= 4 && parts[0] == "MANDIBLE") Hosts[$"{ip}:{parts[3]}"] = (parts[2], _clock);
        }
        var stale = new List<string>();
        foreach (var (k, v) in Hosts) if (_clock - v.seen > 4) stale.Add(k);
        foreach (var k in stale) Hosts.Remove(k);
    }

    public override void _ExitTree() { _udp?.Close(); }
}
