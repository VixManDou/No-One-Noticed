using UnityEngine;

public class NewMonoBehaviourScript : MonoBehaviour
{

    [SerializeField] public float Sensibilidad = 1000;
    public Transform Player;

    float rotacionHorizontal = 0;
    float rotacionVertical = 0;

    void Start()
    {

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        
    }

    // Update is called once per frame
    void Update()
    {

        float Valorx = Input.GetAxis("Mouse X") * Sensibilidad * Time.deltaTime;
        float Valory = Input.GetAxis("Mouse Y") * Sensibilidad * Time.deltaTime;

        rotacionHorizontal += Valorx;
        rotacionVertical -= Valory;

        rotacionVertical = Mathf.Clamp(rotacionVertical, -90, 90);

        transform.localRotation = Quaternion.Euler(rotacionVertical, 0f, 0f);

        Player.Rotate(Vector3.up * Valorx);

    }
}
