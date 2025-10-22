namespace DevelopersHub.ProceduralTilemapGenerator2D.Tools
{
    using UnityEngine;

    public class CharacterController2D : MonoBehaviour
    {

        [SerializeField] private Camera _camera = null;
        [SerializeField] private float _cameraSize = 5;
        [SerializeField] private float _moveSpeed = 2f;
        private Rigidbody2D _rigidbody = null;
        
        private void Awake()
        {
            _camera.orthographic = true;
            _camera.orthographicSize = _cameraSize;
            _rigidbody = GetComponent<Rigidbody2D>();
            _rigidbody.simulated = true;
            _rigidbody.gravityScale = 0;
            _rigidbody.constraints = RigidbodyConstraints2D.FreezeRotation;
        }
        
        private void Update()
        {
            float x = Input.GetAxis("Horizontal");
            float y = Input.GetAxis("Vertical");
            _rigidbody.linearVelocity = new Vector2(x, y) * _moveSpeed;
        }

        private void FixedUpdate()
        {
            _camera.transform.position = transform.position + Vector3.back * 10;
        }
        
    }
}