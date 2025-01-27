using Steamworks;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;
using DiscordPresence;
using DavidFDev.DevConsole;
using Party = Steamworks.Data.Lobby;

public class SteamManager : MonoBehaviour
{
    public static SteamManager Instance { get; private set; }

    // Party Setup
    public Party currentParty;
    public int maximumPlayerCount = 4;
    public string gameId = "myGameId";
    private int privacy; // In accordance with Steamworks API, 0 = Private, 1 = FriendsOnly, 2 = Public, 3 = Invisible (Unused). More info here: https://partner.steamgames.com/doc/api/ISteamMatchmaking#ELobbyType

    // Events
    public static event Action<Party> OnPartyUpdate;
    public static event Action<string, float, Party?> OnPartyNotification;
    public static event Action<Friend, string> OnPartyChatMessage;

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

        random = new System.Random();
        privacy = 1; // FriendsOnly (default)
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
            name: "triggerPartyUpdateCallback",
            aliases: "",
            helpText: "Trigger the callback for a party update to reload dependent systems such as UI",
            callback: () => { 
                OnPartyUpdate?.Invoke(currentParty);
                DevConsole.Log("[SteamManager] Triggered party update callback");
            }
        ));

        DevConsole.AddCommand(Command.Create<ulong>(
            name: "joinParty",
            aliases: "",
            helpText: "Join a Steam party",
            p1: Parameter.Create(
                name: "id",
                helpText: "ID of the Steam party"
            ),
            callback: (ulong id) => JoinParty(id)
        ));

        DevConsole.AddCommand(Command.Create(
            name: "joinRandomParty",
            aliases: "",
            helpText: "Join a random Steam party",
            callback: () => JoinRandomParty()
        ));

        DevConsole.AddCommand(Command.Create(
            name: "leaveParty",
            aliases: "",
            helpText: "Attempt to leave your current party and host a new one instead",
            callback: () => LeaveParty(null, false, true)
        ));

        DevConsole.AddCommand(Command.Create(
            name: "togglePartyPrivacy",
            aliases: "",
            helpText: "Toggle the privacy settings of your party (owner only)",
            callback: () => TogglePartyPrivacy()
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
                        PromotePlayer(null, member);
                        return;
                    }
                }

                DevConsole.Log($"[SteamManager] Couldn't find a Steam user in this party with ID: {id}");
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
                        KickPlayer(null, member);
                        return;
                    }
                }

                DevConsole.Log($"[SteamManager] Couldn't find a Steam user in this party with ID: {id}");
            }
        ));

        DevConsole.AddCommand(Command.Create(
            name: "partyInfo",
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

                DevConsole.Log("************************");
                DevConsole.Log("[SteamManager] Steam Party Info:");
                DevConsole.Log($"[SteamManager] Party ID: {currentParty.Id}");
                DevConsole.Log($"[SteamManager] Party Owner: {currentParty.Owner.Name} ({currentParty.Owner.Id})");
                DevConsole.Log($"[SteamManager] Party Privacy: {sPrivacy} ({privacy})");
                DevConsole.Log($"[SteamManager] Members ({currentParty.MemberCount}/{currentParty.MaxMembers}):");
                foreach (Friend member in currentParty.Members)
                {
                    DevConsole.Log($"[SteamManager] {member.Name} ({member.Id})");
                }
                DevConsole.Log("************************");
            }
        ));

        DevConsole.AddCommand(Command.Create(
            name: "steamInfo",
            aliases: "",
            helpText: "Prints Steam client instance info",
            callback: () => {
                DevConsole.Log("************************");
                DevConsole.Log("[SteamManager] Steam Client Info:");
                DevConsole.Log($"[SteamManager] Name: {SteamClient.Name}");
                DevConsole.Log($"[SteamManager] Steam ID: {SteamClient.SteamId}");
                DevConsole.Log($"[SteamManager] App ID: {SteamClient.AppId}");
                DevConsole.Log($"[SteamManager] User State: {SteamClient.State}");
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
            callback: (string message) => SendChatMessage(null, message)
        ));

        DevConsole.AddCommand(Command.Create(
            name: "copyPartyId",
            aliases: "",
            helpText: "Copy the ID of your current Steam party",
            callback: () => CopyPartyId()
        ));
    }

    private void Notify(string message, float duration, Party? party = null)
    {
        OnPartyNotification?.Invoke(message, duration, party);
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

        if (command == "KICK")
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

        if (command == "UPDATEDOWNER")
        {
            DevConsole.Log($"[SteamManager] {party.Owner.Name} has been promoted to party leader");
            Notify($"{party.Owner.Name} has been promoted to party leader!", 3f);
            OnPartyUpdate?.Invoke(party);
        }
    }

    public void CopyPartyId()
    {
        TextEditor te = new TextEditor();
        te.text = currentParty.Id.ToString();
        te.SelectAll();
        te.Copy();
        DevConsole.Log($"[SteamManager] Copied party ID {currentParty.Id}");
        Notify("Copied Party ID", 2f);
    }

    #endregion

    #region Callbacks

    private void PartyCreated(Result result, Party party)
    {
        if (result == Result.OK)
        {
            party.SetFriendsOnly();
            party.SetJoinable(true);
            party.SetData("game", gameId);
            party.SetData("privacy", "1");
            DevConsole.Log($"[SteamManager] Party successfully created with ID: {party.Id}");
        }
    }

    private void PartyEntered(Party party)
    {
        currentParty = party;
        DevConsole.Log($"[SteamManager] Joined party with ID: {party.Id}");

        if (!party.Owner.IsMe)
            Notify($"Entered {party.Owner.Name}'s party", 2f);

        OnPartyUpdate?.Invoke(party);

        if (int.TryParse(party.GetData("privacy"), out int privacySetting))
        {
            if (privacy == privacySetting) return;

            privacy = privacySetting;

            switch (privacy)
            {
                case 0:
                    DevConsole.Log("[SteamManager] Party privacy changed. Party is now private");
                    Notify("Party is now private. Only way for other players to join is through an invite.", 4f);
                    break;
                case 1:
                    DevConsole.Log("[SteamManager] Party privacy changed. Party is now friends only");
                    Notify("Party is now friends only. Only friends can join your party without an invite.", 4f);
                    break;
                case 2:
                    DevConsole.Log("[SteamManager] Party privacy changed. Party is now public");
                    Notify("Party is now public. Anyone can join your party.", 4f);
                    break;
            }
        }
    }

    private void UserJoinedParty(Party party, Friend friend)
    {
        DevConsole.Log($"[SteamManager] {friend.Name} ({friend.Id}) joined the party");
        Notify($"{friend.Name} joined the party", 2f);
        OnPartyUpdate?.Invoke(party);
    }

    private void UserLeftParty(Party party, Friend friend)
    {
        if (recentlyKicked)
        {
            DevConsole.Log($"[SteamManager] {friend.Name} ({friend.Id}) was kicked from the party");
            Notify($"{friend.Name} was kicked from the party", 2f);
        }
        else
        {
            DevConsole.Log($"[SteamManager] {friend.Name} ({friend.Id}) left the party");
            Notify($"{friend.Name} left the party", 2f); 
        }
        OnPartyUpdate?.Invoke(party);
    }

    private void UserDisconnectedFromParty(Party party, Friend friend)
    {
        DevConsole.Log($"[SteamManager] {friend.Name} ({friend.Id}) has disconnected from the party");
        Notify($"{friend.Name} disconnected the party", 2f);
        OnPartyUpdate?.Invoke(party);
    }

    private void PartyUserDataChanged(Party party, Friend friend)
    {
        DevConsole.Log($"[SteamManager] {friend.Name} ({friend.Id}) changed their user data");
        OnPartyUpdate?.Invoke(party);
    }

    private async void PartyJoinRequested(Party party, SteamId steamId)
    {
        await party.Join();
    }

    private void InvitedToParty(Friend friend, Party party)
    {
        DevConsole.Log($"[SteamManager] Invite recieved from {friend.Name} ({friend.Id}) to join party with ID: {party.Id}");
        Notify($"{friend.Name} invited you to their party.", 6f, party);
    }

    private void ChatMessageRecieved(Party party, Friend friend, string message)
    {
        
        if (message.StartsWith("[SERVERCOMMAND]"))
        {
            DevConsole.Log($"[SteamManager] Server command recieved from {friend.Name} ({friend.Id}): {message.Split(' ')[1]}");
            ProcessServerCommand(party, message, party.IsOwnedBy(friend.Id));
            return;
        }

        OnPartyChatMessage?.Invoke(friend, message);
        DevConsole.Log($"[SteamManager] Chat message recieved from {friend.Name} ({friend.Id}): {message}");
    }

    private void PartyDataChanged(Party party)
    {
        if (int.TryParse(party.GetData("privacy"), out int privacySetting))
        {
            if (privacy == privacySetting) return;

            privacy = privacySetting;

            switch (privacy)
            {
                case 0:
                    DevConsole.Log("[SteamManager] Party privacy changed. Party is now private");
                    Notify("Party is now private. Only way for other players to join is through an invite.", 4f);
                    break;
                case 1:
                    DevConsole.Log("[SteamManager] Party privacy changed. Party is now friends only");
                    Notify("Party is now friends only. Only friends can join your party without an invite.", 4f);
                    break;
                case 2:
                    DevConsole.Log("[SteamManager] Party privacy changed. Party is now public");
                    Notify("Party is now public. Anyone can join your party.", 4f);
                    break;
            }

            OnPartyUpdate?.Invoke(party);
        }
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
            DevConsole.Log("[SteamManager] You are already in this party");
            Notify("You're already in this party.", 3f);
            return;
        }

        if (parties == null)
        {
            DevConsole.Log("[SteamManager] No joinable party with that ID was found");
            Notify("No joinable party with that ID was found!", 3f);
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

        DevConsole.Log("[SteamManager] No joinable party with that ID was found");
        Notify("No joinable party with that ID was found!", 3f);
    }

    public async void JoinRandomParty()
    {
        Party[] parties = await SteamMatchmaking.LobbyList.WithSlotsAvailable(1).WithKeyValue("game", gameId).RequestAsync();

        if (parties == null)
        {
            DevConsole.Log("[SteamManager] No joinable public parties found");
            Notify("There are currently no joinable public parties. Try again later.", 3f);
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
            DevConsole.Log("[SteamManager] No joinable public parties found");
            Notify("There are currently no joinable public parties. Try again later.", 3f);
            return;
        }

        int index = random.Next(parties.Length);
        DevConsole.Log($"[SteamManager] Found {parties.Length} joinable public parties. Joining party with index {index}");
        JoinParty(parties[index].Id);
    }

    public void LeaveParty(Party? party, bool kicked = false, bool allowSoloLeave = false)
    {
        if (party == null)
            party = currentParty;

        if (party.Value.Id == 0) return;

        if (party.Value.MemberCount == 1 && !allowSoloLeave)
        {
            DevConsole.Log("[SteamManager] Can't leave a party when you're the only member left.");
            Notify("Can't leave a party when you're the only member left.", 3f);
            return;
        }

        party.Value.Leave();

        if (kicked)
        {
            DevConsole.Log($"[SteamManager] You were kicked from the party with ID: {party.Value.Id}");
            Notify("You were kicked from the party by the party owner.", 3f);
        }
        else
        {
            DevConsole.Log($"[SteamManager] You left the party with ID: {party.Value.Id}");
            Notify("You left the party", 2f);
        }
        
        HostParty();
    }

    public void KickPlayer(Party? party, Friend friend)
    {
        if (party == null)
            party = currentParty;

        if (!party.Value.Owner.IsMe)
        {
            DevConsole.Log("[SteamManager] Only party owners can kick players");
            return;
        }
        if (friend.IsMe)
        {
            DevConsole.Log("[SteamManager] Cannot kick yourself from the party");
            return;
        }

        party.Value.SendChatString($"[SERVERCOMMAND] KICK {friend.Id}");
    }

    public void PromotePlayer(Party? party, Friend friend)
    {
        if (party == null)
            party = currentParty;

        Party castParty = (Party)party;

        if (!party.Value.Owner.IsMe)
        {
            DevConsole.Log("[SteamManager] Only party owners can promote players");
            return;
        }
        if (friend.IsMe)
        {
            DevConsole.Log("[SteamManager] You are already party owner");
            return;
        }

        castParty.Owner = friend;
        castParty.SendChatString($"[SERVERCOMMAND] UPDATEDOWNER");
    }

    public void SendChatMessage(Party? party, string message)
    {
        if (party == null)
            party = currentParty;

        party.Value.SendChatString(message);
    }

    public void OpenFriendsMenu()
    {
        SteamFriends.OpenOverlay("friends");
    }

    public void OpenInviteFriendsMenu()
    {
        SteamFriends.OpenGameInviteOverlay(currentParty.Id);
    }

    public void TogglePartyPrivacy()
    {
        if (!currentParty.Owner.IsMe)
        {
            DevConsole.Log("[SteamManager] Only the party owner can toggle party privacy settings");
            Notify("Only the party owner can toggle party privacy settings.", 3f);
            return;
        }

        int newPrivacy = privacy + 1;

        if (newPrivacy > 2) newPrivacy = 0;

        switch (newPrivacy)
        {
            case 0:
                currentParty.SetPrivate();
                break;
            case 1:
                currentParty.SetFriendsOnly();
                break;
            case 2:
                currentParty.SetPublic();
                break;
        }

        currentParty.SetData("privacy", newPrivacy.ToString());
    }

    #endregion
}