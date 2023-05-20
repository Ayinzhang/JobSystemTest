using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class ManagePrefabs : MonoBehaviour
{
    GameObject prefab;
    public enum PrefabSelect{ OldDynamicBonePrefab, NewDynamicBonePrefab, MyDynamicBonePrefab }
    public PrefabSelect currentPrefab = PrefabSelect.MyDynamicBonePrefab;

    public void CreatePrefabs()
    {
        switch (currentPrefab)
        {
            case PrefabSelect.OldDynamicBonePrefab:
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/oldDynamicBonePrefab.prefab");
                break;
            case PrefabSelect.NewDynamicBonePrefab:
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/newDynamicBonePrefab.prefab");
                break;
            case PrefabSelect.MyDynamicBonePrefab:
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/myDynamicBonePrefab.prefab");
                break;
        }

        for (int i = -5; i <= 5; ++i)
            for (int j = -5; j <= 5; ++j)
                Instantiate(prefab, transform.position + new Vector3(i, 0, j), transform.rotation, transform);
    }

    public void DestroyPrefabs()
    {
        while (transform.childCount > 0)
            DestroyImmediate(transform.GetChild(0).gameObject);
    }
}
