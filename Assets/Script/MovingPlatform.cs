using UnityEngine;

public class MovingPlatform : MonoBehaviour
{
    public Vector3 pointA;
    public Vector3 pointB;
    public float speed = 2f;

    private Vector3 target;
    private Vector3 lastPosition;
    public Vector3 DeltaMovement { get; private set; }

    void Start()
    {
        pointA = transform.position;
        pointB = transform.position + new Vector3(10f, 0f, 0f); // left-right movement
        target = pointB;
        lastPosition = transform.position;
    }

    void Update()
    {
        transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);

        if (Vector3.Distance(transform.position, target) < 0.05f)
        {
            target = (target == pointA) ? pointB : pointA;
        }

        DeltaMovement = transform.position - lastPosition;
        lastPosition = transform.position;
    }
}