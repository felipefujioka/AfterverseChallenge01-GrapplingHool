using StarterAssets;
using UnityEngine;

public class GrapplingHookController : MonoBehaviour
{

    [SerializeField] ThirdPersonController _characterController;

    private Camera _camera;

    private StarterAssetsInputs _input;

    private int _layerMask;

    private RaycastHit hit;

    private void Start()
    {
        _layerMask = LayerMask.GetMask(new string[] {"Wall"});
        _camera = Camera.main;
        _input = GetComponent<StarterAssetsInputs>();
    }

    void Update()
    {
        if (_input.hook)
        {
            _input.hook = false;
            Ray ray = _camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));

            if (Physics.Raycast(ray, out hit,  100, _layerMask))
            {
                _characterController.HookTo(hit.point, hit.normal);
            }
        }

        if (_input.retract)
        {
            _input.retract = false;

            _characterController.RetractRope();
        }
    }
}
