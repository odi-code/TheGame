using UnityEngine;
using System.Collections;

public class PinState : MonoBehaviour
{
    private PinScore pinScore;
    private bool hasScored = false;
    public int scoreAmount = 1;

    private void Awake()
    {
        pinScore = FindFirstObjectByType<PinScore>();

        if (pinScore == null)
        {
            Debug.LogWarning("pinscore script not found in scene!", this);
        } 
    }

    void Update()
    {
        
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Lane") && !hasScored)
        {
            hasScored = true;
            StartCoroutine(AddScoreAfterDelay(2f));
        }
    }

    private IEnumerator AddScoreAfterDelay(float delay)
    {
        GameObject toDestroy = transform.parent != null ? transform.parent.gameObject : gameObject;
        yield return new WaitForSeconds(delay);
        pinScore?.AddScore(scoreAmount);
        Destroy(toDestroy);
    }
}