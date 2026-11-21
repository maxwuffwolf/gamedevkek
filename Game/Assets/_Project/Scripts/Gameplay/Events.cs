using System.Collections.Generic;
using Game.Gameplay.Actors;
using UnityEngine;

namespace Game.Gameplay
{
    public readonly struct DamageDealtEvent
    {
        public readonly Actor Source;
        public readonly Actor Target;
        public readonly float Amount;
        public readonly bool Crit;
        public readonly Vector3 Position;
        public readonly float ScreenShake;

        public DamageDealtEvent(Actor source, Actor target, float amount, bool crit, Vector3 position, float screenShake)
        {
            Source = source;
            Target = target;
            Amount = amount;
            Crit = crit;
            Position = position;
            ScreenShake = screenShake;
        }
    }

    public readonly struct ActorDiedEvent
    {
        public readonly Actor Actor;
        public readonly Actor Killer;
        public ActorDiedEvent(Actor actor, Actor killer) { Actor = actor; Killer = killer; }
    }

    public readonly struct ActorSpawnedEvent
    {
        public readonly Actor Actor;
        public ActorSpawnedEvent(Actor actor) { Actor = actor; }
    }

    public readonly struct MonsterKilledEvent
    {
        public readonly string MonsterId;
        public readonly IReadOnlyList<string> Tags;
        public readonly int Level;
        public readonly Vector3 Position;
        public readonly Actor Killer;

        public MonsterKilledEvent(string monsterId, IReadOnlyList<string> tags, int level, Vector3 position, Actor killer)
        {
            MonsterId = monsterId;
            Tags = tags;
            Level = level;
            Position = position;
            Killer = killer;
        }
    }

    public readonly struct PlayerSpawnedEvent
    {
        public readonly Actor Player;
        public PlayerSpawnedEvent(Actor player) { Player = player; }
    }
}
