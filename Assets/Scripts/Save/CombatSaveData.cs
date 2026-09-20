using System;
using UnityEngine;

namespace Combat.Save
{
    [Serializable]
    public class GameSaveData
    {
        public string saveTimestamp;
        public string sceneName;
        public PlayerSaveData player = new PlayerSaveData();
        public EnemySaveData enemy = new EnemySaveData();
        public DummySaveData dummy = new DummySaveData();
    }

    [Serializable]
    public class PlayerSaveData
    {
        public float posX;
        public float posY;
        public float health = 100f;
        public float maxHealth = 100f;
        public bool isDead = false;
        public int currentStance = 0; // 0 = Normal, 1 = Tactician
        public float facingDirection = 1f;

        public Vector2 Position
        {
            get => new Vector2(posX, posY);
            set { posX = value.x; posY = value.y; }
        }
    }

    [Serializable]
    public class EnemySaveData
    {
        public float posX;
        public float posY;
        public float health = 100f;
        public float maxHealth = 100f;
        public float stamina = 100f;
        public float maxStamina = 100f;
        public bool isDead = false;
        public float facingDirection = -1f;

        public Vector2 Position
        {
            get => new Vector2(posX, posY);
            set { posX = value.x; posY = value.y; }
        }
    }

    [Serializable]
    public class DummySaveData
    {
        public float posX;
        public float posY;
        public float health = 200f;
        public float maxHealth = 200f;
        public bool isDead = false;

        public Vector2 Position
        {
            get => new Vector2(posX, posY);
            set { posX = value.x; posY = value.y; }
        }
    }
}
