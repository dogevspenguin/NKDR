using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using BDArmory.Settings;

namespace BDArmory.Utils
{
    public class ObjectPool : MonoBehaviour
    {
        public GameObject poolObject;
        public int size { get { return pool.Count; } }
        public bool canGrow;
        public float disableAfterDelay;
        public bool forceReUse;
        public int lastIndex = 0;

        public List<GameObject> pool;

        public string poolObjectName;

        void Awake()
        {
            pool = new List<GameObject>();
        }

        void OnDestroy()
        {
            foreach (var poolObject in pool)
                if (poolObject != null)
                    Destroy(poolObject);
        }

        public GameObject GetPooledObject(int index)
        {
            return pool[index];
        }

        public void AdjustSize(int count)
        {
            if (count > size) // Increase the pool.
                AddObjectsToPool(count - size);
            else
            { // Destroy the excess, then shrink the pool.
                for (int i = count; i < size; ++i)
                {
                    if (pool[i] == null) continue;
                    Destroy(pool[i]);
                }
                pool.RemoveRange(count, size - count);
                lastIndex = 0;
            }
            if (BDArmorySettings.DEBUG_OTHER) Debug.Log("[BDArmory.ObjectPool]: Resizing " + poolObjectName + " pool to " + size);
        }

        private void AddObjectsToPool(int count)
        {
            for (int i = 0; i < count; ++i)
            {
                GameObject obj = Instantiate(poolObject);
                obj.transform.SetParent(transform);
                obj.SetActive(false);
                pool.Add(obj);
            }
        }

        private void ReplacePoolObject(int index)
        {
            Debug.LogWarning("[BDArmory.ObjectPool]: Object of type " + poolObjectName + " was null at position " + index + ", replacing it.");
            GameObject obj = Instantiate(poolObject);
            obj.transform.SetParent(transform);
            obj.SetActive(false);
            pool[index] = obj;
        }

        public GameObject GetPooledObject()
        {
            // Start at the last index returned and cycle round for efficiency. This makes this a typically O(1) seek operation.
            for (int i = lastIndex + 1; i < pool.Count; ++i)
            {
                if (pool[i] == null) // This happens with decals.
                {
                    ReplacePoolObject(i);
                }
                if (!pool[i].activeInHierarchy)
                {
                    lastIndex = i;
                    if (disableAfterDelay > 0f) DisableAfterDelay(pool[i], disableAfterDelay);
                    return pool[i];
                }
            }
            for (int i = 0; i < lastIndex + 1; ++i)
            {
                if (pool[i] == null) // This happens with decals.
                {
                    ReplacePoolObject(i);
                }
                if (!pool[i].activeInHierarchy)
                {
                    lastIndex = i;
                    if (disableAfterDelay > 0f) DisableAfterDelay(pool[i], disableAfterDelay);
                    return pool[i];
                }
            }

            if (canGrow)
            {
                var size = (int)(pool.Count * 1.2) + 1; // Grow by 20% + 1
                if (BDArmorySettings.DEBUG_OTHER) Debug.Log("[BDArmory.ObjectPool]: Increasing pool size to " + size + " for " + poolObjectName);
                AddObjectsToPool(size - pool.Count);

                if (disableAfterDelay > 0f) DisableAfterDelay(pool[pool.Count - 1], disableAfterDelay);
                return pool[pool.Count - 1]; // Return the last entry in the pool
            }

            if (forceReUse) // Return an old entry that is already being used.
            {
                lastIndex = (lastIndex + 1) % pool.Count;
                pool[lastIndex].SetActive(false);
                if (disableAfterDelay > 0f) DisableAfterDelay(pool[lastIndex], disableAfterDelay);
                return pool[lastIndex];
            }

            return null;
        }

        public void DisableAfterDelay(GameObject obj, float t)
        {
            StartCoroutine(DisableObject(obj, t));
        }

        IEnumerator DisableObject(GameObject obj, float t)
        {
            yield return new WaitForSecondsFixed(t);
            if (obj)
            {
                obj.SetActive(false);
                obj.transform.parent = transform;
            }
        }

        public static ObjectPool CreateObjectPool(GameObject obj, int size, bool canGrow, bool destroyOnLoad, float disableAfterDelay = 0f, bool forceReUse = false)
        {
            if (BDArmorySettings.DEBUG_OTHER) Debug.Log("[BDArmory.ObjectPool]: Creating object pool of size " + size + " for " + obj.name);
            GameObject poolObject = new GameObject(obj.name + "Pool");
            ObjectPool op = poolObject.AddComponent<ObjectPool>();
            op.poolObject = obj;
            op.canGrow = canGrow;
            op.disableAfterDelay = disableAfterDelay;
            op.forceReUse = forceReUse;
            op.poolObjectName = obj.name;
            if (!destroyOnLoad)
            {
                DontDestroyOnLoad(poolObject);
            }
            op.AddObjectsToPool(size);

            return op;
        }
    }
}
