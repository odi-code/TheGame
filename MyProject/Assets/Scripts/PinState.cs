using UnityEngine;

public class PinState : MonoBehaviour
{
    private PinScore pinScore;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void Awake()
{
    pinScore = FindFirstObjectByType<PinScore>();

    if (pinScore == null)
    {
        Debug.LogWarning("pinscore script not found in scene!", this);
    } 
}

    // Update is called once per frame
    void Update()
    {
        
    }
    private void OnTriggerEnter(Collider other){
    if (other.CompareTag("Lane"))
    {
        GameObject toDestroy = transform.parent != null ? transform.parent.gameObject : gameObject;
        Destroy(toDestroy, 2f);
    }
    
    }
}

