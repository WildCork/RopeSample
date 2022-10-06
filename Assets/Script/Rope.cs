using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using static UnityEngine.RuleTile.TilingRuleOutput;

public class Rope : MonoBehaviour
{
    public GameObject _player = null;
    public GameObject _aimPoint = null;
    public  bool _isHooked = false;

    [SerializeField] private float _maxOffset = 0f;
    [SerializeField] private float _stepOffset = 0f;
    [SerializeField] private float _cancelVelocityDamp = 0f;
    [SerializeField] private float _minLength = 0f;
    [SerializeField] private float _nowLength = 0f;
    [SerializeField] private float _maxLength = 0f;
    [SerializeField] private float _climbSpeed = 0f;
    [SerializeField] private float _swingVelocity = 0f;
    [SerializeField] private Material _material;


    private Rigidbody2D _playerBody = null;
    private int _mapLayer = -1;
    private List<Vector2> _anchorsList = new List<Vector2>();
    private Vector2 _preAnchorDir = Vector2.zero;
    private Vector2 _nextAnchorDir = Vector2.zero;

    private DistanceJoint2D _playerDistanceJoint = null;
    private LineRenderer _ropeLineRenderer = null;
    private List<float> _combinedAnchorLen = new List<float>();
    private RaycastHit2D _firstHitRaycast2D;

    public bool CanThrow
    {
        get
        {
            if (_player)
            {
                return Vector2.Distance(_player.transform.position, transform.position) < _maxLength;
            }
            else
            {
                Debug.LogWarning("현재 이 로프에는 주인이 없습니다");
                return false;
            }
        }
    }

    private void Awake()
    {
        _playerDistanceJoint = _player.GetComponent<DistanceJoint2D>();
        _playerDistanceJoint.connectedBody = GetComponent<Rigidbody2D>();
        _ropeLineRenderer = gameObject.GetComponent<LineRenderer>();
        _ropeLineRenderer.material = _material;
        _mapLayer = 1 << LayerMask.NameToLayer("Map");
    }

    private void OnEnable()
    {
        _playerDistanceJoint.anchor = Vector2.zero;

        _playerBody = _player.GetComponent<Rigidbody2D>();
        _aimPoint.SetActive(false);

        _ropeLineRenderer.enabled = true;
        _ropeLineRenderer.positionCount = 2;
        _ropeLineRenderer.SetPosition(0, Vector2.zero);
        _ropeLineRenderer.SetPosition(1, Vector2.zero);

        _combinedAnchorLen.Clear();
        _anchorsList.Clear();
    }
    private void OnDisable()
    {
        _playerDistanceJoint.enabled = false;

        if (_isHooked)
        {
            Vector2 swingNormalDir = SwingNormalDir(_player.transform.position);
            _playerBody.velocity = swingNormalDir * Vector2.Dot(swingNormalDir, _playerBody.velocity) * _cancelVelocityDamp;
        }
        _aimPoint.SetActive(true);

        _isHooked = false;
        _ropeLineRenderer.enabled = false;
    }

    private void Update()
    {
        ManageRopeLine();

        if (!_isHooked)
        {
            TryHook();
        }
        else
        {
            ManageAnchors();
            ControlRope();
            ForceCancel();
        }
    }

    private void TryHook()
    {
        if (_firstHitRaycast2D = Physics2D.Linecast(_player.transform.position, transform.position, _mapLayer))
        {
            CompleteHook(HookedPos());
        }
    }

    private Vector2 HookedPos()
    {
        for (int i = 1; i < (int)(_maxOffset / _stepOffset) + 1; i++)
        {
            if (!Physics2D.Linecast(_player.transform.position, _firstHitRaycast2D.point + _stepOffset * i * _firstHitRaycast2D.normal.normalized, _mapLayer))
            {
                return _firstHitRaycast2D.point + (_firstHitRaycast2D.normal.normalized * _stepOffset * i);
            }
        }
        return _firstHitRaycast2D.point + (_firstHitRaycast2D.normal.normalized * _maxOffset);
    }
    private void CompleteHook(Vector2 firstHitPos)
    {
        transform.position = firstHitPos;
        _isHooked = true;
        _playerDistanceJoint.distance = _nowLength = Vector2.Distance(transform.position, _player.transform.position);
        _playerDistanceJoint.enabled = true;
        AddAnchor(firstHitPos);
    }

    private void ManageAnchors()
    {
        transform.position = _anchorsList.Last();

        if (ShouldAddAnchor())
        {
            AddAnchor(_nextAnchorDir);
        }

        if (CanDeleteAnchor())
        {
            DeleteAnchor();
        }
    }

    private void ManageRopeLine()
    {
        if (_anchorsList.Count == 0)
        {
            _ropeLineRenderer.SetPosition(0, transform.position);
            _ropeLineRenderer.SetPosition(1, _player.transform.position);
        }
        else
        {
            _ropeLineRenderer.SetPosition(0, _anchorsList.First());
            _ropeLineRenderer.SetPosition(_anchorsList.Count, _player.transform.position);
        }
    }
    private bool ShouldAddAnchor()
    {
        RaycastHit2D hit;
        if (hit = Physics2D.Linecast(_player.transform.position, _anchorsList.Last(), _mapLayer))
        {
            for (int i = 1; i < (int)(_maxOffset / _stepOffset) + 1; i++)
            {
                Vector2 nextPos = hit.point + _stepOffset * i * hit.normal.normalized;
                if (!Physics2D.Linecast(_player.transform.position, nextPos, _mapLayer) && !Physics2D.Linecast(nextPos, _anchorsList.Last(), _mapLayer))
                {
                    _nextAnchorDir = hit.point + _stepOffset * i * hit.normal.normalized;
                    return true;
                }
            }
        }
        return false;
    }

    private bool CanDeleteAnchor()
    {
        if (_anchorsList.Count < 2)
        {
            return false;
        }
        _preAnchorDir = _anchorsList[_anchorsList.Count - 2];
        Vector2 _lastAnchorDir = _anchorsList.Last();
        if (Physics2D.Linecast(_player.transform.position, _preAnchorDir, _mapLayer) || Physics2D.Linecast(_player.transform.position, _lastAnchorDir, _mapLayer))
            return false;
        else
            return true;
    }
    private void ControlRope()
    {
        if (Input.GetAxisRaw("Vertical") != 0 || Input.GetAxisRaw("Horizontal") != 0)
        {
            Climb();
            Swing();
        }
        else
        {
            _playerBody.velocity = Vector2.zero;
        }
    }

    private void Climb()
    {
        _nowLength -= Input.GetAxisRaw("Vertical") * _climbSpeed * Time.deltaTime;
        if(_nowLength >  _maxLength)
        {
            _nowLength = _maxLength;
        }
        else if(_nowLength < _minLength)
        {
            _nowLength = _minLength;
        }
        _playerDistanceJoint.distance = _nowLength - _combinedAnchorLen.Sum();
    }

    private void Swing()
    {
        if (Input.GetAxisRaw("Horizontal") != 0)
        {
            Vector2 swingDir = SwingNormalDir(_player.transform.position);
            _playerBody.velocity = (_swingVelocity * _nowLength) * swingDir * Input.GetAxisRaw("Horizontal") * Time.deltaTime;
        }
    }

    private Vector2 SwingNormalDir(Vector2 ownerPos)
    {
        Vector2 lastAnchorPosFromOwner = _anchorsList.Last() - ownerPos;
        return (Quaternion.AngleAxis(-90, Vector3.forward) * lastAnchorPosFromOwner).normalized;
    }

    private void AddAnchor(Vector2 pos)
    {
        _anchorsList.Add(pos);
        if (_anchorsList.Count > 1)
        {
            _combinedAnchorLen.Add(Vector2.Distance(_anchorsList.Last(), _anchorsList[_anchorsList.Count - 2]));
        }
        SetLastAnchor();
    }

    private void DeleteAnchor()
    {
        _combinedAnchorLen.RemoveAt(_combinedAnchorLen.Count - 1);
        _anchorsList.RemoveAt(_anchorsList.Count - 1);
        SetLastAnchor();
    }

    private void SetLastAnchor()
    {
        _playerDistanceJoint.distance = Vector2.Distance(_player.transform.position, _anchorsList.Last());
        _playerDistanceJoint.anchor = Vector2.zero;
        _playerDistanceJoint.connectedAnchor = Vector2.zero;
        DrawRopeLine();
    }

    private void ForceCancel()
    {
        if (Vector2.Distance(transform.position, _player.transform.position) + _combinedAnchorLen.Sum() > _maxLength)
        {
            Debug.Log("Force To Cancel Rope");
            _player.GetComponent<Player>().CancelRope();
        }
    }

    private void DrawRopeLine()
    {
        _ropeLineRenderer.positionCount = _anchorsList.Count + 1;
        _ropeLineRenderer.SetPosition(_anchorsList.Count - 1, _anchorsList.Last());
        _ropeLineRenderer.SetPosition(_anchorsList.Count, _player.transform.position);
    }
}
