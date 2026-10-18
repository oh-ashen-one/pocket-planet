using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PocketPlanet
{
    public class WaterRippleAnimator : MonoBehaviour
    {
        private float _t;
        private void Update()
        {
            _t += Time.deltaTime * 2.4f;
            float s = Mathf.Lerp(0.28f, 1.85f, _t);
            transform.localScale = new Vector3(s, 0.008f * (1f - _t), s);
            if (_t >= 1f)
                Destroy(gameObject);
        }
    }
}
