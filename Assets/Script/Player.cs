using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player : MonoBehaviour
{
    private Rigidbody2D _rigidbody;
    private Collider2D _collider;
    private FixedJoint2D _fixedJoint;

    [SerializeField] private bool _isJump = false;
    [SerializeField] private bool _isUseRope = false;
    [SerializeField] private bool _isRopeHook = false;

    [SerializeField] private float _speed = 0f;
    void Start()
    {
        Screen.SetResolution(1920, 1080, true);
        _rigidbody = GetComponent<Rigidbody2D>();
        _collider = GetComponent<Collider2D>();
        _fixedJoint = GetComponent<FixedJoint2D>();
    }


    void Update()
    {
        if(!_isUseRope)
        {
            Move(Input.GetAxisRaw("Horizontal"));
            Aim(Input.GetAxisRaw("Vertical"));
        }
        if(Input.GetKeyDown(KeyCode.Space))
        {
            if (_isUseRope)
            {
                CancelRope();
            }
            else
            {
                ShootRope((_aimPoint.transform.position - transform.position).normalized);
            }
        }
    }
    private void Move(float horizon)
    {
        Walk(horizon);
        if(horizon != 0)
        {
            Turn(horizon);
        }
    }

    Vector3 _localScale;
    void Turn(float horizon)
    {
        _localScale = transform.localScale;
        _localScale.x = horizon;
        transform.localScale = _localScale;
    }

    Vector2 vel;
    void Walk(float horizon)
    {
        vel = _rigidbody.velocity;
        vel.x = _speed * horizon;
        _rigidbody.velocity = vel;
    }

    [SerializeField] private float _aimAngleSpeed;
    [SerializeField] private GameObject _aimPoint;
    private void Aim(float yInput)
    {
        if (yInput != 0)
        {
            _aimPoint.transform.RotateAround(transform.position, Vector3.forward, _aimAngleSpeed * yInput * (transform.localScale.x > 0 ? 1 : -1) * Time.deltaTime);
        }
    }


    [SerializeField] private Rope _rope = null;
    [SerializeField] private float _shootSpeed = 0f;

    public void CancelRope()
    {
        StopAllCoroutines();

        _isUseRope = false;
        _rope.gameObject.SetActive(false);
    }

    private void ShootRope(Vector2 aimDir)
    {
        _isUseRope = true;

        _rope.transform.position = transform.position;
        _rope.gameObject.SetActive(true);
        _rope.GetComponent<Rigidbody2D>().velocity = aimDir * _shootSpeed;

        StartCoroutine(Throw());
    }

    [SerializeField] private bool _isThrowOut = false;
    [SerializeField] private WaitForSeconds c_waitTime = new WaitForSeconds(0.1f);
    IEnumerator Throw()
    {
        _isThrowOut = true;
        for (int i = 0; i < 50 && _rope.CanThrow; i++)
        {
            if (_rope._isHooked)
            {
                _isThrowOut = false;
                break;
            }
            yield return c_waitTime;
        }
        if (_isThrowOut)
        {
            CancelRope();
        }
    }
}
