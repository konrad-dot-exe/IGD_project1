// Assets/Scripts/Core/RunStats.cs
namespace EarFPS
{
    public struct RunStats
    {
        public int   score;
        public float timeSeconds;
        public int   enemiesDestroyed;
        public int   correct;
        public int   total;
        public int   bestStreak;

        public float Accuracy01 => total > 0 ? (float)correct / total : 0f;
    }
}
