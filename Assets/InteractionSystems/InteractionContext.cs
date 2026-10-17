using UnityEngine;

public struct InteractionContext
{
    public GameObject Interactor { get; }
    public float MovementSpeed { get; }

    public InteractionContext(GameObject interactor, float movementSpeed = 0f)
    {
        Interactor = interactor;
        MovementSpeed = movementSpeed;
    }
}