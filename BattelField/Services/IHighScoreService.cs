using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BattelField.Services
{
    public interface IHighScoreService
    {
        void SaveScore(int shots);
        int GetBestScore();
    }

    public class FileHighScoreService : IHighScoreService
    {
        private readonly string _filePath = "highscore.txt";

        public void SaveScore(int shots)
        {
            int currentBest = GetBestScore();

            // In Battleship, a lower number of shots is a better score
            if (shots < currentBest || currentBest == 0)
            {
                File.WriteAllText(_filePath, shots.ToString());
            }
        }

        public int GetBestScore()
        {
            if (!File.Exists(_filePath)) return 0;

            string content = File.ReadAllText(_filePath);
            return int.TryParse(content, out int score) ? score : 0;
        }
    }
}
