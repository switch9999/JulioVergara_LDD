namespace DevelopersHub.ProceduralTilemapGenerator2D.Tools
{
    using UnityEngine;

    public class CameraSortMode2D : MonoBehaviour
    {

        private void Awake()
        {
            var _camera = GetComponent<Camera>();
            if (_camera != null)
            {
                _camera.transparencySortMode = TransparencySortMode.CustomAxis;
                _camera.transparencySortAxis = Vector3.up;
            }
        }
        
    }
}