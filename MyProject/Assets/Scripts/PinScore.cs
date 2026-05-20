using UnityEngine;

public class PinScore : MonoBehaviour
{
    public int score;


public void AddScore(int amount)
    {
        score = amount + score;
    }
}
