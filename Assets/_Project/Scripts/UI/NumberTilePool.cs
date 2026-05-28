using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MathGame.UI
{
    /// <summary>
    /// Manages the bottom row of number tiles the player taps to fill blanks.
    /// Also owns the backspace button.
    /// </summary>
    public class NumberTilePool : MonoBehaviour
    {
        [SerializeField] private NumberTile   _tilePrefab;
        [SerializeField] private Transform    _tileContainer;
        [SerializeField] private Button       _backspaceButton;

        // Set by QuestionPanel so tiles can notify it
        public QuestionPanel QuestionPanel { get; set; }

        private readonly List<NumberTile> _activeTiles = new();

        private void Awake()
        {
            _backspaceButton.onClick.AddListener(OnBackspaceClicked);
        }

        public void SetupPool(int[] numbers)
        {
            ClearPool();

            foreach (int number in numbers)
            {
                NumberTile tile = Instantiate(_tilePrefab, _tileContainer);
                tile.Setup(number, OnTileClicked);
                _activeTiles.Add(tile);
            }
        }

        public void ResetAllTiles()
        {
            foreach (var tile in _activeTiles)
                tile.SetUsed(false);
        }

        private void ClearPool()
        {
            foreach (var tile in _activeTiles)
                if (tile != null) Destroy(tile.gameObject);
            _activeTiles.Clear();
        }

        private void OnTileClicked(NumberTile tile)
        {
            QuestionPanel?.FillNextBlank(tile.Value, tile);
        }

        private void OnBackspaceClicked()
        {
            QuestionPanel?.ClearLastBlank();
        }
    }
}
