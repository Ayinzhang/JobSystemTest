using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class CreatePrefabs : MonoBehaviour
{
    GameObject prefab;
    enum PrefabSelect{ Old, New, My }
    PrefabSelect currentPrefab = PrefabSelect.My;

    void OnGUI()
    {
        //EditorGUILayout.Separator();
        currentPrefab = (PrefabSelect)EditorGUILayout.EnumPopup("Ԥ��������", currentPrefab);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("����Ԥ����"))
        {
            switch (currentPrefab)
            {
                case PrefabSelect.Old:
                    prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/oldDynamicBonePrefab.prefab");
                    break;
                case PrefabSelect.New:
                    prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/newDynamicBonePrefab.prefab");
                    break;
                case PrefabSelect.My:
                    prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/myDynamicBonePrefab.prefab");
                    break;
            }

            for (int i = -10; i <= 10; ++i)
                for (int j = -10; j <= 10; ++j)
                    Instantiate(prefab, transform.position + new Vector3(i, 0, j), transform.rotation, transform);
        }
        if(GUILayout.Button("����Ԥ����"))
        {
            for (int i = 0; i < transform.childCount; ++i)
                Destroy(transform.GetChild(i).gameObject);
        }
        GUILayout.EndHorizontal();
    }
}
