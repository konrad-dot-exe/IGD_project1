using UnityEngine;

namespace EarFPS
{
    public class HomingMissile : MonoBehaviour
    {
        [SerializeField] float speed = 30f;
        [SerializeField] float turnRateDeg = 360f;
        [SerializeField] float explodeRadius = 1.5f;
        [SerializeField] GameObject explodeVFX;

        EnemyShip target;

        public void Init(EnemyShip t) { target = t; }

        void Update()
        {
            if (target == null) { Destroy(gameObject); return; }
            Vector3 to = (target.transform.position - transform.position).normalized;
            transform.forward = Vector3.RotateTowards(transform.forward, to, Mathf.Deg2Rad * turnRateDeg * Time.deltaTime, 1f);
            transform.position += transform.forward * speed * Time.deltaTime;

            if ((target.transform.position - transform.position).magnitude <= explodeRadius)
            {
                if (explodeVFX) Instantiate(explodeVFX, target.transform.position, Quaternion.identity);
                target.Die();
                Destroy(gameObject);
            }
        }
    }
}
