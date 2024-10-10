using Microsoft.AspNetCore.SignalR;
using Microsoft.Identity.Client;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;
using System.Text.RegularExpressions;
using TypingGameApp.API.Models;
using TypingGameApp.API.Service;
using static System.Net.Mime.MediaTypeNames;
using static TypingGameApp.API.Hubs.GameHub;

namespace TypingGameApp.API.Hubs
{
    public class GameHub : Hub
    {

        private readonly TextService _textService;
        private static readonly ConcurrentDictionary<string, Lobby> Lobbies = new ConcurrentDictionary<string, Lobby>();

        public GameHub(TextService textService)
        {
            _textService = textService ?? throw new ArgumentNullException(nameof(textService));
        }

        public async Task CreateLobby(string username, string difficulty, int timer)
        {
            string playerId = Context.ConnectionId;
            var lobbyId = Guid.NewGuid().ToString();

            var existingLobby = Lobbies.Values.FirstOrDefault(lobby => lobby.Creator.ConnectionId == playerId);

            if (existingLobby != null)
            {
                await Clients.Caller.SendAsync("LobbyCreationError", "You already have an active lobby. Delete it before creating a new one");
                return;
            }

            var user = new UserConnection { ConnectionId = playerId, UserName = username };

            Lobbies[lobbyId] = new Lobby
            {
                Creator = user,
                Difficulty = difficulty,
                Timer = timer,
                Players = new List<UserConnection>(),
                GameInProgress = false
            };

            Lobbies[lobbyId].Players.Add(user);
            await Groups.AddToGroupAsync(playerId, lobbyId);
            await Clients.Caller.SendAsync("CreateLobby", Lobbies[lobbyId], lobbyId);
            Console.WriteLine($"Count of lobbies: {Lobbies.Count}");
        }

        public async Task DeleteLobby(string lobbyId)
        {
            if (Lobbies.TryGetValue(lobbyId, out var lobby))
            {
                // Only the lobby creator can delete the lobby
                if (lobby.Creator.ConnectionId != Context.ConnectionId)
                {
                    await Clients.Caller.SendAsync("LobbySuccessError", "Only the lobby creator can delete the lobby.");
                    return;
                }

                // Notify players that the lobby is being deleted
                await Clients.Group(lobbyId).SendAsync("LobbyDeletedMessage", "The lobby has been deleted by the creator.");

                // Remove all players from the group and delete the lobby
                foreach (var player in lobby.Players)
                {
                    await Groups.RemoveFromGroupAsync(player.ConnectionId, lobbyId);
                }

                if (Lobbies.TryRemove(lobbyId, out _))
                {
                    await Clients.Caller.SendAsync("LobbyDeletedSuccessfully", "The lobby was deleted successfully.");
                }
                else
                {
                    await Clients.Caller.SendAsync("LobbyDeletedError", "Failed to delete the lobby.");
                }
            }
            Console.WriteLine($"Count of lobbies:{Lobbies.Count}");
        }

        // Add player to lobby and return list of players in lobby
        public async Task JoinLobby(string username, string lobbyId)
        {
            if (Lobbies.ContainsKey(lobbyId))
            {
                var existingConnection = Lobbies[lobbyId].Players.FirstOrDefault(player => player.ConnectionId == Context.ConnectionId);
                if (existingConnection == null)
                {
                    var user = new UserConnection { ConnectionId = Context.ConnectionId, UserName = username };
                    Lobbies[lobbyId].Players.Add(user);
                    await Groups.AddToGroupAsync(Context.ConnectionId, lobbyId);
                    await Clients.Group(lobbyId).SendAsync("PlayerJoined", Lobbies[lobbyId], username, lobbyId);
                    Console.WriteLine($"Count of user in lobbies:{Lobbies[lobbyId].Players.Count}");
                }
                else
                {
                    await Clients.Caller.SendAsync("PlayerConnectionExist", "You already have a connection to the lobby.");
                }
            }
        }

        public async Task LeaveLobby(string username, string lobbyId)
        {
            if (Lobbies.ContainsKey(lobbyId))
            {
                var lobbyWithPlayer = Lobbies.Values.FirstOrDefault(lobby =>
                    lobby.Players.Any(connection => connection.ConnectionId == Context.ConnectionId));
                if (lobbyWithPlayer != null)
                {
                    var existingConnection = lobbyWithPlayer.Players.FirstOrDefault(connection => connection.ConnectionId == Context.ConnectionId);

                    if (existingConnection != null)
                    {
                        Lobbies[lobbyId].Players.Remove(existingConnection);
                        await Clients.Group(lobbyId).SendAsync("PlayerLeft", Lobbies[lobbyId], username, lobbyId);
                        await Groups.RemoveFromGroupAsync(Context.ConnectionId, lobbyId);
                        Console.WriteLine($"Count of user in lobbies:{Lobbies[lobbyId].Players.Count}");
                    }
                }
            }
        }

        public async Task StartGame(string lobbyId)
        {
            if (Lobbies.ContainsKey(lobbyId))
            {
                var lobby = Lobbies[lobbyId];
                if (lobby.Creator.ConnectionId != Context.ConnectionId)
                {
                    await Clients.Caller.SendAsync("GameStartSuccessError", "Only the lobby creator can start the game.");
                    return;
                }

                if (lobby.GameInProgress == true)
                {
                    await Clients.Caller.SendAsync("GameInProgressError", "the game is still in progress or not everyone has left the game");
                    return;
                }

                lobby.GameInProgress = true;
                string text = await _textService.GetRandomTextAsync(lobby.Difficulty);
                await Clients.Group(lobbyId).SendAsync("StartGame", text, lobby.Timer);
                await Clients.Group(lobbyId).SendAsync("StartGameMessage", "Game is started!!!!!!!");
            }
        }

        public async Task SubmitResult(string lobbyId, string username, int wpm)
        {
            if (Lobbies.ContainsKey(lobbyId))
            {
                Lobbies[lobbyId].PlayerResults[username] = wpm;

                // Broadcast results when all players have finished
                if (Lobbies[lobbyId].PlayerResults.Count == Lobbies[lobbyId].Players.Count)
                {
                    var winner = Lobbies[lobbyId].PlayerResults.OrderByDescending(r => r.Value).First();
                    await Clients.Group(lobbyId).SendAsync("GameEnded", winner.Key, winner.Value);

                    Lobbies[lobbyId].PlayerResults.Clear();
                    Lobbies[lobbyId].GameInProgress = false;
                }
            }
        }

        public class UserConnection
        {
            public string ConnectionId { get; set; }
            public string UserName { get; set; }
        }

        public class Lobby
        {
            public UserConnection Creator { get; set; }
            public string Difficulty { get; set; }
            public int Timer { get; set; }
            public List<UserConnection> Players { get; set; }
            public Dictionary<string, int> PlayerResults { get; set; } = new Dictionary<string, int>();
            public bool GameInProgress { get; set; }
        }
    }
}
