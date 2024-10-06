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

        public async Task CreateLobby(string username, string difficulty, int timer)
        {
            string playerId = Context.ConnectionId;
            var lobbyId = Guid.NewGuid().ToString();

            var user = new UserConnection { ConnectionId = playerId, Name = username };

            if (Lobbies.ContainsKey(playerId))
            {
                await Clients.Caller.SendAsync("Error", "You have active lobby.");
                return;
            }

            Lobbies[lobbyId] = new Lobby 
            {
                Creator = user,
                Difficulty = difficulty, 
                Timer = timer,
                Players = new List<string>(),
                GameInProgress = false
            };

            
            Lobbies[lobbyId].Players.Add(Context.ConnectionId);
            await Groups.AddToGroupAsync(Context.ConnectionId, lobbyId);
            await Clients.Caller.SendAsync("CreateLobby", lobbyId);
            Console.WriteLine($"Count of lobbies:{Lobbies.Count}");
        }

        public async Task DeleteLobby(string lobbyId)
        {
            if (Lobbies.TryGetValue(lobbyId, out var lobby))
            {
                // Only the lobby creator can delete the lobby
                if (lobby.Creator.ConnectionId != Context.ConnectionId)
                {
                    await Clients.Caller.SendAsync("Error", "Only the lobby creator can delete the lobby.");
                    return;
                }

                // Notify players that the lobby is being deleted
                await Clients.Group(lobbyId).SendAsync("LobbyDeleted", "The lobby has been deleted by the creator.");

                // Remove all players from the group and delete the lobby
                foreach (var player in lobby.Players)
                {
                    await Groups.RemoveFromGroupAsync(player, lobbyId);
                }

                if (Lobbies.TryRemove(lobbyId, out _))
                {
                    await Clients.Caller.SendAsync("LobbyDeletedSuccessfully", "The lobby was deleted successfully.");
                }
                else
                {
                    await Clients.Caller.SendAsync("Error", "Failed to delete the lobby.");
                }
            }
            Console.WriteLine($"Count of lobbies:{Lobbies.Count}");
        }

        // Add player to lobby and return list of players in lobby
        public async Task JoinLobby(string lobbyId)
        {
            if (Lobbies.ContainsKey(lobbyId))
            {
                Lobbies[lobbyId].Players.Add(Context.ConnectionId);
                await Clients.Group(lobbyId).SendAsync("PlayerJoined", Context.ConnectionId);
                await Groups.AddToGroupAsync(Context.ConnectionId, lobbyId);
                await Clients.Group(lobbyId).SendAsync("UpdatePlayerList", Lobbies[lobbyId].Players);
            }
        }

        public async Task LeaveLobby(string lobbyId)
        {
            if (Lobbies.ContainsKey(lobbyId))
            {
                Lobbies[lobbyId].Players.Remove(Context.ConnectionId);
                await Clients.Group(lobbyId).SendAsync("PlayerLeft", Context.ConnectionId);
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, lobbyId);
            }
        }

        public async Task StartGame(string lobbyId)
        {
            if (Lobbies.ContainsKey(lobbyId))
            {
                var lobby = Lobbies[lobbyId];
                lobby.GameInProgress = true;
                string text = await _textService.GetRandomTextAsync(lobby.Difficulty);
                await Clients.Group(lobbyId).SendAsync("StartGame", text, lobby.Timer);
            }
        }

        public async Task SubmitResult(string lobbyId, string playerId, int wpm)
        {
            if (Lobbies.ContainsKey(lobbyId))
            {
                Lobbies[lobbyId].PlayerResults[playerId] = wpm;

                // Broadcast results when all players have finished
                if (Lobbies[lobbyId].PlayerResults.Count == Lobbies[lobbyId].Players.Count)
                {
                    var winner = Lobbies[lobbyId].PlayerResults.OrderByDescending(r => r.Value).First();
                    await Clients.Group(lobbyId).SendAsync("GameEnded", winner.Key, winner.Value);
                }
            }
        }

        public class UserConnection
        {
            public string ConnectionId { get; set; }
            public string Name { get; set; }
        }

        public class Lobby
        {
            public UserConnection Creator { get; set; }
            public string Difficulty { get; set; }
            public int Timer { get; set; }
            public List<string> Players { get; set; }
            public Dictionary<string, int> PlayerResults { get; set; } = new Dictionary<string, int>();
            public bool GameInProgress { get; set; }
        }
    }
}
