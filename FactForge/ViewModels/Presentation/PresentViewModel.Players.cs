using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using FactForge.Models;
using FactForge.Services;
using FactForge.Services.Presentation;

namespace FactForge.ViewModels.Presentation;

//Players

public partial class PresentViewModel
{
    // Thread-safe queue for incoming players to prevent UI thread flooding during massive joins
    private readonly ConcurrentQueue<PlayerInfoDto> _pendingPlayers = new();

    private void OnPlayerJoined(PlayerInfoDto player)
    {
        // Safely queue from background threads without locking the network thread
        _pendingPlayers.Enqueue(player);
    }

    private void OnPlayerDisconnected(PlayerInfoDto player)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var index = -1;

            for (var i = 0; i < Players.Count; i++)
            {
                if (Players[i].Id == player.Id)
                {
                    index = i;
                    break;
                }
            }

            if (index >= 0)
                Players[index] = player;
        });
    }

    private void FlushPendingPlayers()
    {
        if (_pendingPlayers.IsEmpty) return;

        Dispatcher.UIThread.Post(() =>
        {
            int addedCount = 0;
            // Process up to 50 players per tick to prevent UI locking during heavy join floods
            while (_pendingPlayers.TryDequeue(out var player) && addedCount < 50)
            {
                var index = -1;

                for (var i = 0; i < Players.Count; i++)
                {
                    if (Players[i].Id == player.Id)
                    {
                        index = i;
                        break;
                    }
                }

                if (index >= 0)
                    Players[index] = player;
                else
                    Players.Add(player);

                addedCount++;
            }
        });
    }
}