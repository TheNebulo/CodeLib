using Steamworks;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;
using DavidFDev.DevConsole;
using Unity.Netcode;
using System.Threading.Tasks;

using Party = Steamworks.Data.Lobby;

public class SteamManager : MonoBehaviour
{
    public static SteamManager Instance { get; private set; }

    // Party Setup
    public Party currentParty;
    public int minimumStartPlayerCount = 1;
    public int maximumPlayerCount = 4;
    public bool overrideTransportStartConditions = false;
    public string gameId = "myGameId";
    private int privacy; // In accordance with Steamworks API, 0 = Private, 1 = FriendsOnly, 2 = Public, 3 = Invisible (Unused). More info here: https://partner.steamgames.com/doc/api/ISteamMatchmaking#ELobbyType
    private bool joinable;

    // Events
    public static event Action<Party> OnPartyUpdate;
    public static event Action<string, float, Party?> OnPartyNotification;
    public static event Action<Friend, string> OnPartyChatMessage;
    public static event Action<string, IEnumerable<string>, bool> OnServerCommand;

    // Misc
    private System.Random random;
    private bool recentlyKicked = false;

    private void Awake()
    {
        if (Instance == null) { Instance = this; }
        else if (Instance != this) { Destroy(gameObject); }
    }

    void OnEnable()
    {
        DontDestroyOnLoad(gameObject);

        // Adding Steam Matchmaking callbacks
        SteamMatchmaking.OnLobbyCreated += PartyCreated;
        SteamMatchmaking.OnLobbyEntered += PartyEntered;
        SteamMatchmaking.OnLobbyMemberJoined += UserJoinedParty;
        SteamMatchmaking.OnLobbyMemberDataChanged += PartyUserDataChanged;
        SteamMatchmaking.OnLobbyMemberDisconnected += UserDisconnectedFromParty;
        SteamMatchmaking.OnLobbyMemberLeave += UserLeftParty;
        SteamFriends.OnGameLobbyJoinRequested += PartyJoinRequested;
        SteamMatchmaking.OnChatMessage += ChatMessageRecieved;
        SteamMatchmaking.OnLobbyInvite += InvitedToParty;
        SteamMatchmaking.OnLobbyDataChanged += PartyDataChanged;

        SetupDevCommands();
        OnPartyUpdate += (party) => { currentParty = party; };

        random = new System.Random();
        HostParty();
    }

    void OnDisable()
    {
        // Clearing Steam Matchmaking callbacks
        SteamMatchmaking.OnLobbyCreated -= PartyCreated;
        SteamMatchmaking.OnLobbyEntered -= PartyEntered;
        SteamMatchmaking.OnLobbyMemberJoined -= UserJoinedParty;
        SteamMatchmaking.OnLobbyMemberDataChanged -= PartyUserDataChanged;
        SteamMatchmaking.OnLobbyMemberDisconnected -= UserDisconnectedFromParty;
        SteamMatchmaking.OnLobbyMemberLeave -= UserLeftParty;
        SteamFriends.OnGameLobbyJoinRequested -= PartyJoinRequested;
        SteamMatchmaking.OnLobbyInvite -= InvitedToParty;
        SteamMatchmaking.OnChatMessage -= ChatMessageRecieved;
        SteamMatchmaking.OnLobbyDataChanged -= PartyDataChanged;
    }

    #region Utility Functions

    private void SetupDevCommands()
    {
        DevConsole.AddCommand(Command.Create(
            name: "trigger_party_update_callbacks",
            aliases: "",
            helpText: "Trigger the callback for a party update to reload dependent systems such as UI",
            callback: () => { 
                OnPartyUpdate?.Invoke(currentParty);
                DevConsole.LogSuccess("[SteamManager] Triggered party update callback");
            }
        ));

        DevConsole.AddCommand(Command.Create<ulong>(
            name: "join_party",
            aliases: "",
            helpText: "Join a Steam party",
            p1: Parameter.Create(
                name: "id",
                helpText: "ID of the Steam party"
            ),
            callback: (ulong id) => JoinParty(id)
        ));

        DevConsole.AddCommand(Command.Create(
            name: "join_random_party",
            aliases: "",
            helpText: "Join a random Steam party",
            callback: () => JoinRandomParty()
        ));

        DevConsole.AddCommand(Command.Create(
            name: "leave_party",
            aliases: "",
            helpText: "Attempt to leave your current party and host a new one instead",
            callback: () => LeaveParty(null, false, true)
        ));

        DevConsole.AddCommand(Command.Create(
            name: "start_transport",
            aliases: "",
            helpText: "Start the active network transport",
            callback: async () => await StartTransport()
        ));

        DevConsole.AddCommand(Command.Create(
            name: "shutdown_trasnport",
            aliases: "",
            helpText: "Shutdown the active network transport",
            callback: () => ShutdownTransport()
        ));

        DevConsole.AddCommand(Command.Create<bool>(
            name: "override_transport_start_conditions",
            aliases: "",
            helpText: "Override the conditions required for a match start",
            p1: Parameter.Create(
                name: "setOverride",
                helpText: "Whether to override the conditions"
            ),
            callback: (bool setOverride) => {
                overrideTransportStartConditions = setOverride;
                DevConsole.Log($"[SteamManager] Set transport start conditions override to {overrideTransportStartConditions}");
            }
        ));

        DevConsole.AddCommand(Command.Create<int?>(
            name: "set_party_privacy",
            aliases: "",
            helpText: "Set the privacy settings of your party (owner only)",
            p1: Parameter.Create(
                name: "privacy",
                helpText: "Specific privacy setting (0: Private, 1: Friends Only, 2: Public, ~: Next)"
            ),
            callback: (int? privacySetting) => {
                if (!privacySetting.HasValue) TogglePartyPrivacy();
                else if (privacySetting.Value < 0 || privacySetting.Value > 2) DevConsole.LogError("[SteamManager] Privacy settings must be between 0 and 2!");
                else TogglePartyPrivacy(privacySetting.Value);
            }
        ));

        DevConsole.AddCommand(Command.Create<ulong>(
            name: "promote",
            aliases: "",
            helpText: "Promote a player from the Steam party (owner only)",
            p1: Parameter.Create(
                name: "id",
                helpText: "ID of the user to promote"
            ),
            callback: (ulong id) => {
                foreach (Friend member in currentParty.Members)
                {
                    if (member.Id.Value == id)
                    {
                        PromotePlayer(member);
                        return;
                    }
                }

                DevConsole.LogError($"[SteamManager] Couldn't find a Steam user in this party with ID: {id}");
            }
        ));

        DevConsole.AddCommand(Command.Create<ulong>(
            name: "kick",
            aliases: "",
            helpText: "Kick a player from the Steam party (ownerOnly)",
            p1: Parameter.Create(
                name: "id",
                helpText: "ID of the user to kick"
            ),
            callback: (ulong id) => {
                foreach (Friend member in currentParty.Members)
                {
                    if (member.Id.Value == id)
                    {
                        KickPlayer(member);
                        return;
                    }
                }

                DevConsole.LogError($"[SteamManager] Couldn't find a Steam user in this party with ID: {id}");
            }
        ));

        DevConsole.AddCommand(Command.Create(
            name: "party_info",
            aliases: "",
            helpText: "Prints Steam party info",
            callback: () =>
            {
                string sPrivacy = "Invisible";

                switch (privacy)
                {
                    case 0:
                        sPrivacy = "Private";
                        break;
                    case 1:
                        sPrivacy = "Friends Only";
                        break;
                    case 2:
                        sPrivacy = "Public";
                        break;
                }

                DevConsole.Log("[SteamManager] Steam Party Info:");
                DevConsole.Log("************************");
                DevConsole.Log($"Party ID: {currentParty.Id}");
                DevConsole.Log($"Party Owner: {currentParty.Owner.Name} ({currentParty.Owner.Id})");
                DevConsole.Log($"Party Privacy: {sPrivacy} ({privacy})");
                DevConsole.Log($"Party Joinable: {joinable}");
                DevConsole.Log($"Members ({currentParty.MemberCount}/{currentParty.MaxMembers}):");
                foreach (Friend member in currentParty.Members)
                {
                    DevConsole.Log($"{member.Name} ({member.Id})");
                }
                DevConsole.Log("************************");
            }
        ));

        DevConsole.AddCommand(Command.Create(
            name: "steam_info",
            aliases: "",
            helpText: "Prints Steam client instance info",
            callback: () => {
                DevConsole.Log("[SteamManager] Steam Client Info:");
                DevConsole.Log("************************");
                DevConsole.Log($"Name: {SteamClient.Name}");
                DevConsole.Log($"Steam ID: {SteamClient.SteamId}");
                DevConsole.Log($"App ID: {SteamClient.AppId}");
                DevConsole.Log($"User State: {SteamClient.State}");
                DevConsole.Log("************************");
            }
        ));

        DevConsole.AddCommand(Command.Create<string>(
            name: "send",
            aliases: "say",
            helpText: "Send a chat message in your current party",
            p1: Parameter.Create(
                name: "message",
                helpText: "Message to send"
            ),
            callback: (string message) => SendChatMessage(message)
        ));

        DevConsole.AddCommand(Command.Create<string>(
            name: "send_server_command",
            aliases: "",
            helpText: "Send a server command in your current party",
            p1: Parameter.Create(
                name: "command",
                helpText: "Command to send (with arguments)"
            ),
            callback: (string command) => SendServerCommand(command)
        ));

        DevConsole.AddCommand(Command.Create(
            name: "copy_party_id",
            aliases: "",
            helpText: "Copy the ID of your current Steam party",
            callback: () => CopyPartyId()
        ));
    }

    private void ProcessServerCommand(Party party, string message, bool fromOwner)
    {
        string[] splitMessage = message.Split(' ');
        if (splitMessage.Length < 2) return;

        string command = splitMessage[1];

        IEnumerable<string> arguments = null;
        if (splitMessage.Length > 2)
        {
            arguments = splitMessage.Skip(2);
        }

        if (command == "kick")
        {
            if (!fromOwner) return;

            if (arguments == null || !arguments.Any()) return;

            string steamIdStr = arguments.ElementAt(0);

            if (ulong.TryParse(steamIdStr, out ulong steamIdValue))
            {
                SteamId steamIdToKick = new SteamId();
                steamIdToKick.Value = steamIdValue;

                if (steamIdToKick == SteamClient.SteamId)
                {
                    LeaveParty(party, true);
                }
                else
                    recentlyKicked = true;
            }
        }

        else if (command == "updatedOwner")
        {
            if (!fromOwner) return; // Check if this is properly implemented

            DevConsole.LogSuccess($"[SteamManager] {party.Owner.Name} has been promoted to party leader");
            OnPartyNotification?.Invoke($"{party.Owner.Name} has been promoted to party leader!", 3f, null);
            OnPartyUpdate?.Invoke(party);
        }

        else if (command == "startTransport")
        {
            if (!fromOwner) return;
            if (party.Owner.IsMe) return;
            CustomNetworkManager.Instance.StartClient(party);
        }

        else
        {
            OnServerCommand?.Invoke(command, arguments, fromOwner);
        }
    }

    public void CopyPartyId()
    {
        TextEditor te = new TextEditor();
        te.text = currentParty.Id.ToString();
        te.SelectAll();
        te.Copy();
        DevConsole.LogSuccess($"[SteamManager] Copied party ID {currentParty.Id}");
        OnPartyNotification?.Invoke("Copied Party ID", 2f, null);
    }

    public static Texture2D ConvertSteamImage(Steamworks.Data.Image image)
    {
        var avatar = new Texture2D((int)image.Width, (int)image.Height, TextureFormat.ARGB32, false);
        avatar.filterMode = FilterMode.Trilinear;

        for (int x = 0; x < image.Width; x++)
        {
            for (int y = 0; y < image.Height; y++)
            {
                var p = image.GetPixel(x, y);
                avatar.SetPixel(x, (int)image.Height - y, new UnityEngine.Color(
                    p.r / 255.0f, p.g / 255.0f, p.b / 255.0f, p.a / 255.0f));
            }
        }

        avatar.Apply();
        return avatar;
    }

    public Friend GetSelf(Party? party = null)
    {
        if (party == null)
            party = currentParty;

        return party.Value.Members.ElementAt(party.Value.Members.TakeWhile(friend => !friend.IsMe).Count());
    }

    public Friend GetFirstFriendNotSelf(Party? party = null)
    {
        if (party == null)
            party = currentParty;

        return party.Value.Members.ElementAt(party.Value.Members.TakeWhile(friend => friend.IsMe).Count());
    }

    public Friend? GetFriendById(ulong id, Party? party = null)
    {
        if (party == null)
            party = currentParty;

        Friend friend = party.Value.Members.ElementAt(party.Value.Members.TakeWhile(friend => friend.Id.Value != id).Count());
        if (friend.Id.Value != id) return null;

        return friend;
    }

    #endregion

    #region Callbacks

    private void PartyCreated(Result result, Party party)
    {
        if (result == Result.OK)
        {
            privacy = 2; // Public (default)
            party.SetPublic();
            party.SetData("privacy", "2");

            joinable = true;
            party.SetJoinable(true);
            party.SetData("joinable", "true");

            party.SetData("game", gameId);
            DevConsole.LogSuccess($"[SteamManager] Party successfully created with ID: {party.Id}");
        }
    }

    private void PartyEntered(Party party)
    {
        currentParty = party;
        DevConsole.LogSuccess($"[SteamManager] Joined party with ID: {party.Id}");

        if (!party.Owner.IsMe)
            OnPartyNotification?.Invoke($"Entered {party.Owner.Name}'s party", 2f, null);

        PartyDataChanged(party);
    }

    private void UserJoinedParty(Party party, Friend friend)
    {
        DevConsole.Log($"[SteamManager] {friend.Name} ({friend.Id}) joined the party");
        OnPartyNotification?.Invoke($"{friend.Name} joined the party", 2f, null);
        OnPartyUpdate?.Invoke(party);
    }

    private void UserLeftParty(Party party, Friend friend)
    {
        if (recentlyKicked)
        {
            DevConsole.Log($"[SteamManager] {friend.Name} ({friend.Id}) was kicked from the party");
            OnPartyNotification?.Invoke($"{friend.Name} was kicked from the party", 2f, null);
        }
        else
        {
            DevConsole.Log($"[SteamManager] {friend.Name} ({friend.Id}) left the party");
            OnPartyNotification?.Invoke($"{friend.Name} left the party", 2f, null); 
        }

        OnPartyUpdate?.Invoke(party);
    }

    private void UserDisconnectedFromParty(Party party, Friend friend)
    {
        DevConsole.Log($"[SteamManager] {friend.Name} ({friend.Id}) has disconnected from the party");
        OnPartyNotification?.Invoke($"{friend.Name} disconnected the party", 2f, null);
        OnPartyUpdate?.Invoke(party);
    }

    private void PartyUserDataChanged(Party party, Friend friend)
    {
        DevConsole.Log($"[SteamManager] {friend.Name} ({friend.Id}) changed their user data");
        OnPartyUpdate?.Invoke(party);
    }

    private async void PartyJoinRequested(Party party, SteamId steamId)
    {
        if (party.Id == currentParty.Id)
        {
            DevConsole.LogWarning("[SteamManager] You are already in this party");
            OnPartyNotification?.Invoke("You're already in this party.", 3f, null);
            return;
        }

        await party.Join();
    }

    private void InvitedToParty(Friend friend, Party party)
    {
        DevConsole.Log($"[SteamManager] Invite recieved from {friend.Name} ({friend.Id}) to join party with ID: {party.Id}");
        OnPartyNotification?.Invoke($"{friend.Name} invited you to their party.", 6f, party);
    }

    private void ChatMessageRecieved(Party party, Friend friend, string message)
    {
        
        if (message.StartsWith("[SERVERCOMMAND]"))
        {
            if (!friend.IsMe) DevConsole.Log($"[SteamManager] Server command recieved from {friend.Name} ({friend.Id}): {message.Split(' ')[1]}");
            ProcessServerCommand(party, message, party.IsOwnedBy(friend.Id));
            return;
        }
        
        DevConsole.Log($"[SteamManager] Chat message recieved from {friend.Name} ({friend.Id}): {message}");
        OnPartyChatMessage?.Invoke(friend, message);
    }

    private void PartyDataChanged(Party party)
    {
        if (int.TryParse(party.GetData("privacy"), out int privacySetting))
        {
            if (privacy != privacySetting)
            {
                privacy = privacySetting;

                switch (privacy)
                {
                    case 0:
                        DevConsole.LogSuccess("[SteamManager] Party privacy changed. Party is now private");
                        OnPartyNotification?.Invoke("Party is now private. Only way for other players to join is through an invite.", 4f, null);
                        break;
                    case 1:
                        DevConsole.LogSuccess("[SteamManager] Party privacy changed. Party is now friends only");
                        OnPartyNotification?.Invoke("Party is now friends only. Only friends can join your party without an invite.", 4f, null);
                        break;
                    case 2:
                        DevConsole.LogSuccess("[SteamManager] Party privacy changed. Party is now public");
                        OnPartyNotification?.Invoke("Party is now public. Anyone can join your party.", 4f, null);
                        break;
                }
            }
        }

        if (bool.TryParse(party.GetData("joinable"), out bool joinableSetting))
        {
            if (joinableSetting != joinable)
            {
                if (joinableSetting) DevConsole.LogSuccess("[SteamManager] Party is now joinable");
                else DevConsole.LogSuccess("[SteamManager] Party privacy changed. Party is no longer joinable");

                joinable = joinableSetting;
            }
        }

        OnPartyUpdate?.Invoke(party);
    }

    #endregion

    #region Party (Steam Lobby) Functions

    public async void HostParty()
    {
        await SteamMatchmaking.CreateLobbyAsync(maximumPlayerCount);
    }

    public async void JoinParty(ulong Id)
    {
        Party[] parties = await SteamMatchmaking.LobbyList.WithSlotsAvailable(1).WithKeyValue("game", gameId).RequestAsync();

        if (Id == currentParty.Id)
        {
            DevConsole.LogWarning("[SteamManager] You are already in this party");
            OnPartyNotification?.Invoke("You're already in this party.", 3f, null);
            return;
        }

        if (parties == null)
        {
            DevConsole.LogError("[SteamManager] No joinable party with that ID was found");
            OnPartyNotification?.Invoke("No joinable party with that ID was found!", 3f, null);
            return;
        }

        foreach (Party party in parties)
        {
            if (party.Id == Id)
            {
                await party.Join();
                return;
            }
        }

        DevConsole.LogError("[SteamManager] No joinable party with that ID was found");
        OnPartyNotification?.Invoke("No joinable party with that ID was found!", 3f, null);
    }

    public async void JoinRandomParty()
    {
        Party[] parties = await SteamMatchmaking.LobbyList.WithSlotsAvailable(1).WithKeyValue("game", gameId).RequestAsync();

        if (parties == null)
        {
            DevConsole.LogError("[SteamManager] No joinable public parties found");
            OnPartyNotification?.Invoke("There are currently no joinable public parties. Try again later.", 3f, null);
            return;
        }

        for (int i = 0; i < parties.Length; i++)
        {
            if (parties[i].Id == currentParty.Id)
            {
                var partyList = new List<Party>(parties);
                partyList.RemoveAt(i);
                parties = partyList.ToArray();
                break;
            }
        }

        if (parties == null || !parties.Any())
        {
            DevConsole.LogError("[SteamManager] No joinable public parties found");
            OnPartyNotification?.Invoke("There are currently no joinable public parties. Try again later.", 3f, null);
            return;
        }

        int index = random.Next(parties.Length);
        DevConsole.LogSuccess($"[SteamManager] Found {parties.Length} joinable public parties. Joining party with index {index}");
        JoinParty(parties[index].Id);
    }

    public void LeaveParty(Party? party, bool kicked = false, bool allowSoloLeave = false)
    {
        if (party == null)
            party = currentParty;

        if (party.Value.Id == 0) return;

        if (party.Value.MemberCount == 1 && !allowSoloLeave)
        {
            DevConsole.LogWarning("[SteamManager] Can't leave a party when you're the only member left.");
            OnPartyNotification?.Invoke("Can't leave a party when you're the only member left.", 3f, null);
            return;
        }

        if (CustomNetworkManager.Instance.IsRunning()) CustomNetworkManager.Instance.Shutdown();
        party.Value.Leave();

        if (kicked)
        {
            DevConsole.LogWarning($"[SteamManager] You were kicked from the party with ID: {party.Value.Id}");
            OnPartyNotification?.Invoke("You were kicked from the party by the party owner.", 3f, null);
        }
        else
        {
            DevConsole.LogSuccess($"[SteamManager] You left the party with ID: {party.Value.Id}");
            OnPartyNotification?.Invoke("You left the party", 2f, null);
        }
        
        HostParty();
    }

    public void KickPlayer(Friend friend, Party? party = null)
    {
        if (party == null)
            party = currentParty;

        if (!party.Value.Owner.IsMe)
        {
            DevConsole.LogError("[SteamManager] Only party owners can kick players");
            return;
        }
        if (friend.IsMe)
        {
            DevConsole.LogWarning("[SteamManager] Cannot kick yourself from the party");
            return;
        }

        SendServerCommand($"kick {friend.Id}", party);
    }

    public void PromotePlayer(Friend friend, Party? _party = null)
    {
        if (_party == null)
            _party = currentParty;

        Party party = (Party)_party;

        if (!party.Owner.IsMe)
        {
            DevConsole.LogError("[SteamManager] Only party owners can promote players");
            return;
        }
        if (friend.IsMe)
        {
            DevConsole.LogWarning("[SteamManager] You are already party owner");
            return;
        }

        party.Owner = friend;
        SendServerCommand("updatedOwner", party);
    }

    public async Task StartTransport(Party? party = null)
    {
        if (party == null)
            party = currentParty;

        if (!party.Value.Owner.IsMe)
        {
            DevConsole.LogError("[SteamManager] Only party owners can start transport");
            return;
        }

        if (CustomNetworkManager.Instance.IsRunning())
        {
            DevConsole.LogWarning("[SteamManager] Transport is already running");
            return;
        }

        if (!overrideTransportStartConditions)
        {
            if (party.Value.MemberCount < minimumStartPlayerCount)
            {
                DevConsole.LogError($"[SteamManager] Need at least {minimumStartPlayerCount} players to start transport");
                return;
            }
        }

        TogglePartyJoinable(false);
        CustomNetworkManager.Instance.StartHost(party.Value);
        SendServerCommand("startTransport");

        async Task WaitForClientsToConnect(Party party)
        {
            while (NetworkManager.Singleton.ConnectedClients.Count < party.MemberCount)
            {
                await Task.Yield();
            }
        }

        DevConsole.Log("[SteamManager] Waiting for all clients to connect...");
        await WaitForClientsToConnect(party.Value);
        DevConsole.LogSuccess("[SteamManager] All clients connected");
    }

    public void ShutdownTransport(Party? party = null)
    {
        if (party == null)
            party = currentParty;

        if (!party.Value.Owner.IsMe)
        {
            DevConsole.LogError("[SteamManager] Only party owners can shutdown transport");
            return;
        }

        if (!CustomNetworkManager.Instance.IsRunning())
        {
            DevConsole.LogWarning("[SteamManager] Transport isn't running");
            return;
        }

        TogglePartyJoinable(true);
        CustomNetworkManager.Instance.Shutdown();
    }

    public void SendChatMessage(string message, Party? party = null)
    {
        if (party == null)
            party = currentParty;

        message = message.Trim(' ');

        if (message.Length > 0)
            party.Value.SendChatString(message);
    }

    public void SendServerCommand(string command, Party? party = null)
    {
        if (party == null)
            party = currentParty;

        SendChatMessage($"[SERVERCOMMAND] {command}", party);
    }

    public void OpenFriendsMenu()
    {
        SteamFriends.OpenOverlay("friends");
    }

    public void OpenInviteFriendsMenu()
    {
        SteamFriends.OpenGameInviteOverlay(currentParty.Id);
    }

    public void TogglePartyPrivacy(int? specificPrivacy = null, Party? _party = null)
    {
        if (_party == null)
            _party = currentParty;

        Party party = (Party)_party;

        if (!party.Owner.IsMe)
        {
            DevConsole.LogError("[SteamManager] Only the party owner can toggle party privacy settings");
            OnPartyNotification?.Invoke("Only the party owner can toggle party privacy settings.", 3f, null);
            return;
        }

        int newPrivacy;
        newPrivacy = privacy + 1;

        if (specificPrivacy != null && specificPrivacy >= 0 && specificPrivacy <= 2) 
            newPrivacy = (int)specificPrivacy;

        if (newPrivacy > 2) newPrivacy = 0;

        switch (newPrivacy)
        {
            case 0:
                party.SetPrivate();
                break;
            case 1:
                party.SetFriendsOnly();
                break;
            case 2:
                party.SetPublic();
                break;
        }

        party.SetData("privacy", newPrivacy.ToString());
    }

    public void TogglePartyJoinable(bool? isJoinable = null, Party? _party = null)
    {
        if (_party == null)
            _party = currentParty;

        Party party = (Party)_party;

        if (!party.Owner.IsMe)
        {
            DevConsole.LogError("[SteamManager] Only the party owner can toggle party joinability settings");
            OnPartyNotification?.Invoke("Only the party owner can toggle party joinability settings.", 3f, null);
            return;
        }

        if (isJoinable == joinable) return;

        party.SetJoinable(!joinable);
        party.SetData("joinable", (!joinable).ToString().ToLower());
    }

    #endregion
}