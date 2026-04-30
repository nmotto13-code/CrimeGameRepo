#if UNITY_EDITOR
using System.Linq;
using CasebookGame.Data;
using UnityEditor;
using UnityEngine;

namespace CasebookGame.Editor
{
    public static class ProgressionAssetBootstrapper
    {
        const string DepartmentsFolder = "Assets/Resources/Departments";
        const string DefaultDepartmentAssetPath = DepartmentsFolder + "/Patrol_Training.asset";

        [MenuItem("Casebook/Progression/Ensure Default Department Asset")]
        public static void EnsureDefaultDepartmentAssetMenu() => EnsureDefaultDepartmentAssets();

        public static void EnsureDefaultDepartmentAssets()
        {
            EnsureFolder("Assets/Resources");
            EnsureFolder(DepartmentsFolder);

            var existing = AssetDatabase.FindAssets("t:DepartmentData", new[] { DepartmentsFolder });
            if (existing.Length > 0)
                return;

            var cases = AssetDatabase.FindAssets("t:CaseData", new[] { "Assets/Resources/Cases" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<CaseData>)
                .Where(c => c != null)
                .OrderBy(c => c.caseId)
                .ToArray();

            var department = ScriptableObject.CreateInstance<DepartmentData>();
            department.departmentId = DepartmentId.Patrol;
            department.displayName = "Patrol (Training)";
            department.requiredRank = 1;
            department.requiredStarsCount = 0;
            department.caseIds = cases.Select(c => c.caseId).ToList();

            AssetDatabase.CreateAsset(department, DefaultDepartmentAssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            var parts = path.Split('/');
            string parent = string.Join("/", parts, 0, parts.Length - 1);
            AssetDatabase.CreateFolder(parent, parts[parts.Length - 1]);
        }
    }
}
#endif
