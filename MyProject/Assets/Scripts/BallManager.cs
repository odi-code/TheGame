using UnityEngine;

public class BallManager : MonoBehaviour
{
  
    public GameObject ball;
    public float forceMagnitude = 10f;

    public void Throw()
    {
        Rigidbody rb = ball.GetComponent<Rigidbody>();

        if (rb == null)
        {
            Debug.LogWarning("BallThrower: The provided GameObject does not have a Rigidbody component.");
            return;
        }

        rb.AddForce(Vector3.forward * forceMagnitude, ForceMode.Impulse);
    }
}
