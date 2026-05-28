using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace MathGame.UI
{
    /// <summary>
    /// Full-width horizontal progress bar that shrinks as time runs out.
    /// Changes color from green → yellow → red when under 30 / 10 seconds.
    /// </summary>
    public class TimerBar : MonoBehaviour
    {
        [SerializeField] private Image           _fillImage;
        [SerializeField] private TextMeshProUGUI _timeLabel;

        [Header("Colors")]
        [SerializeField] private Color _colorNormal  = new Color(0.2f, 0.8f, 0.3f);
        [SerializeField] private Color _colorWarning = new Color(1.0f, 0.8f, 0.1f);
        [SerializeField] private Color _colorDanger  = new Color(0.9f, 0.2f, 0.2f);

        private float _totalDuration;

        public void SetDuration(float seconds)
        {
            _totalDuration = seconds;
            UpdateVisuals(seconds);
        }

        public void UpdateTime(float secondsRemaining)
        {
            UpdateVisuals(secondsRemaining);
        }

        private void UpdateVisuals(float secondsRemaining)
        {
            float ratio = _totalDuration > 0f
                ? Mathf.Clamp01(secondsRemaining / _totalDuration)
                : 0f;

            _fillImage.fillAmount = ratio;

            _fillImage.color = secondsRemaining <= 10f ? _colorDanger
                             : secondsRemaining <= 30f ? _colorWarning
                             : _colorNormal;

            int s = Mathf.CeilToInt(secondsRemaining);
            _timeLabel.text = $"{s / 60:00}:{s % 60:00}";
        }
    }
}
