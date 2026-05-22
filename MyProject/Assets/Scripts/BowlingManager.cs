using UnityEngine;
using TMPro;
public class BowlingManager : MonoBehaviour
{
    private PinScore pinScore;
    public TextMeshProUGUI gameScore;


private void Awake()
    {
        pinScore = FindFirstObjectByType<PinScore>();

        if (pinScore == null)
        {
            Debug.LogWarning("pinscore script not found in scene!", this);
        } 
    }

public void Update()
    {
        gameScore.text = pinScore.score.ToString();
    }
    
}
