using System;
using System.Collections.Generic;
using Il2CppEffectors;
using Il2CppLVA.Limbs;
using Il2CppLVA.Limbs.Shape;
using Il2CppLVA.Limbs.Variants.Human;
using Il2CppLVA.Organs;
using Il2CppVoxelMeshGeneration;
using Il2CppVoxelMeshGeneration.Chunks;
using Il2CppVoxelMeshGeneration.Tools;
using MelonLoader;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using Backbone = Il2CppLVA.Organs.Variants.Backbone;
using Bone = Il2CppLVA.Organs.Variants.Human.Bone;
using Brain = Il2CppLVA.Organs.Variants.Brain;
using Eye = Il2CppLVA.Organs.Variants.Eye;
using Heart = Il2CppLVA.Organs.Variants.Heart;
using Lens = Il2CppLVA.Organs.Variants.Lens;
using Liver = Il2CppLVA.Organs.Variants.Liver;
using Lung = Il2CppLVA.Organs.Variants.Lung;
using Rib = Il2CppLVA.Organs.Variants.Rib;
using Skull = Il2CppLVA.Organs.Variants.Skull;
using Stomach = Il2CppLVA.Organs.Variants.Stomach;
using TestOrganPiska = Il2CppLVA.Organs.Variants.TestOrganPiska;
using Object = UnityEngine.Object;

[assembly: MelonInfo(typeof(XRay.XRayMod), "X-Ray", "1.0.0", "github.com/Phoenix557")]
[assembly: MelonGame("tripledose", "FRUKT")]
[assembly: MelonAdditionalDependencies("Phx")]

namespace XRay
{
    public class XRayMod : MelonMod
    {
        const string VisualName = "XRayBones";

        bool _bonesOn;
        bool _organsOn;
        float _refreshAt;
        bool _warned;
        Phx.Hotkey _bonesKey;
        Phx.Hotkey _organsKey;
        readonly Dictionary<int, LimbBones> _limbs = new Dictionary<int, LimbBones>();

        public override void OnInitializeMelon()
        {
            Phx.ModEntry mod = Phx.Mods.Register("X-Ray");
            _bonesKey = mod.Key("Bones", Key.X);
            _organsKey = mod.Key("Organs", Key.O);
            LoggerInstance.Msg("Loaded. Press " + _bonesKey.Current + " for bones and " + _organsKey.Current + " for organs. Both can be on together. Change them under PHX PAUSE in the pause menu.");
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            Clear();
            _bonesOn = false;
            _organsOn = false;
            _warned = false;
            XRayBar.Reset();
        }

        public override void OnUpdate()
        {
            if (_bonesKey != null && _bonesKey.Pressed())
                ToggleBones();
            if (_organsKey != null && _organsKey.Pressed())
                ToggleOrgans();

            XRayBar.Tick(_bonesKey, _organsKey, _bonesOn, _organsOn, ToggleBones, ToggleOrgans);

            if (_bonesOn || _organsOn)
            {
                HoldHidden();
                if (Time.unscaledTime >= _refreshAt)
                {
                    _refreshAt = Time.unscaledTime + 0.5f;
                    Refresh();
                }
            }
        }

        void ToggleBones()
        {
            _bonesOn = !_bonesOn;
            ApplyView();
        }

        void ToggleOrgans()
        {
            _organsOn = !_organsOn;
            ApplyView();
        }

        void ApplyView()
        {
            Clear();
            if (!_bonesOn && !_organsOn)
            {
                LoggerInstance.Msg("Skin restored.");
                return;
            }

            _warned = false;
            Refresh();
            string kind = _bonesOn && _organsOn ? "Bones and organs" : _organsOn ? "Organs" : "Bones";
            LoggerInstance.Msg(_limbs.Count > 0 ? kind + " shown." : "X-Ray is on. " + kind + " will show when a body is here.");
        }

        void Refresh()
        {
            HumanLimb[] limbs = Object.FindObjectsByType<HumanLimb>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            var alive = new HashSet<int>();
            int shown = 0;
            if (limbs != null)
            {
                for (int i = 0; i < limbs.Length; i++)
                {
                    HumanLimb limb = limbs[i];
                    if (limb == null)
                        continue;
                    int id = limb.GetInstanceID();
                    alive.Add(id);
                    if (!_limbs.TryGetValue(id, out LimbBones bones))
                    {
                        bones = new LimbBones();
                        _limbs[id] = bones;
                    }
                    if (bones.Sync(limb, _bonesOn, _organsOn))
                        shown++;
                }
            }

            var gone = new List<int>();
            foreach (var pair in _limbs)
            {
                if (!alive.Contains(pair.Key) || pair.Value.Limb == null)
                    gone.Add(pair.Key);
            }
            for (int i = 0; i < gone.Count; i++)
            {
                if (_limbs.TryGetValue(gone[i], out LimbBones bones))
                    bones.Restore();
                _limbs.Remove(gone[i]);
            }

            if (shown == 0 && _limbs.Count > 0 && !_warned)
            {
                _warned = true;
                LoggerInstance.Warning(_organsOn && !_bonesOn
                    ? "X-Ray could not find organs inside the body."
                    : _bonesOn && !_organsOn
                        ? "X-Ray could not find bones inside the body."
                        : "X-Ray could not find bones or organs inside the body.");
            }
        }

        void HoldHidden()
        {
            foreach (var pair in _limbs)
                pair.Value.KeepHidden();
        }

        void Clear()
        {
            foreach (var pair in _limbs)
                pair.Value.Restore();
            _limbs.Clear();
        }

        sealed class LimbBones
        {
            public HumanLimb Limb;
            GameObject _visual;
            Mesh _mesh;
            VoxelMesh _voxelMesh;
            int _enabledCount = -1;
            readonly List<MeshRenderer> _hidden = new List<MeshRenderer>();
            readonly List<bool> _wasForcedOff = new List<bool>();
            readonly List<Material> _materials = new List<Material>();
            readonly List<MaterialPropertyBlock> _blocks = new List<MaterialPropertyBlock>();
            bool _showBones;
            bool _showOrgans;
            readonly List<int3> _bones = new List<int3>();
            readonly HashSet<int> _boneIds = new HashSet<int>();
            readonly List<Vector3> _vertices = new List<Vector3>();
            readonly List<Vector3> _normals = new List<Vector3>();
            readonly List<Vector2> _uvs = new List<Vector2>();
            readonly List<int> _triangles = new List<int>();

            public bool Sync(HumanLimb limb, bool bones, bool organs)
            {
                if (_showBones != bones || _showOrgans != organs)
                {
                    _showBones = bones;
                    _showOrgans = organs;
                    _enabledCount = -1;
                }

                Limb = limb;
                LimbEffectorReceiver receiver = limb.GetComponent<LimbEffectorReceiver>();
                if (receiver == null)
                    receiver = limb.GetComponentInChildren<LimbEffectorReceiver>();
                VoxelMesh mesh = receiver != null ? receiver.VoxelMesh : null;
                if (mesh == null || mesh.Data == null || limb.References == null || limb.References.ShapeDataHandler == null)
                    return false;
                _voxelMesh = mesh;

                IReadonlyLimbShapeDataHandler shape = limb.References.ShapeDataHandler;
                int enabled = shape.EnabledVoxelsCount;
                if (_visual != null && enabled == _enabledCount)
                {
                    HideFlesh(limb);
                    return _vertices.Count > 0;
                }

                Build(limb, mesh, shape);
                _enabledCount = enabled;
                if (_vertices.Count == 0)
                    return false;

                HideFlesh(limb);
                return true;
            }

            void Build(HumanLimb limb, VoxelMesh mesh, IReadonlyLimbShapeDataHandler shape)
            {
                _bones.Clear();
                _boneIds.Clear();
                _vertices.Clear();
                _normals.Clear();
                _uvs.Clear();
                _triangles.Clear();

                var data = mesh.Data;
                int3 size = data.Size;
                for (int x = 0; x < size.x; x++)
                {
                    for (int y = 0; y < size.y; y++)
                    {
                        for (int z = 0; z < size.z; z++)
                        {
                            var index = new int3(x, y, z);
                            if (data.IsIndexOutOfRange(index) || !data[index].enabled)
                                continue;
                            if (!shape.TryGetOrganByVoxelIndex(index, out AbstractOrgan organ) || !Matches(organ))
                                continue;
                            _bones.Add(index);
                            _boneIds.Add(Pack(index));
                        }
                    }
                }

                if (_bones.Count == 0)
                {
                    DropVisual();
                    return;
                }

                Vector3 pitch = Pitch(mesh);
                Vector3 half = pitch * 0.5f;
                Transform space = mesh.transform;
                for (int i = 0; i < _bones.Count; i++)
                {
                    int3 index = _bones[i];
                    Vector3 center = space.InverseTransformPoint(VoxelTools.VoxelIndexToWorldPosition(mesh, index));
                    Vector2 uv = UV(data[index].color);
                    for (int axis = 0; axis < 3; axis++)
                    {
                        TryFace(index, center, half, uv, axis, 1);
                        TryFace(index, center, half, uv, axis, -1);
                    }
                }

                if (_triangles.Count == 0)
                {
                    DropVisual();
                    return;
                }

                if (_visual == null)
                {
                    _visual = new GameObject(VisualName);
                    _visual.transform.SetParent(space, false);
                    _visual.AddComponent<MeshFilter>();
                    MeshRenderer renderer = _visual.AddComponent<MeshRenderer>();
                    renderer.shadowCastingMode = ShadowCastingMode.On;
                    renderer.receiveShadows = true;
                    Material source = FleshMaterial(limb);
                    if (source != null)
                        renderer.sharedMaterial = source;
                }

                if (_mesh == null)
                {
                    _mesh = new Mesh();
                    _mesh.name = VisualName;
                    _mesh.indexFormat = IndexFormat.UInt32;
                }
                else
                {
                    _mesh.Clear();
                }

                _mesh.vertices = _vertices.ToArray();
                _mesh.normals = _normals.ToArray();
                _mesh.uv = _uvs.ToArray();
                _mesh.triangles = _triangles.ToArray();
                _mesh.RecalculateBounds();
                _visual.GetComponent<MeshFilter>().sharedMesh = _mesh;
                _visual.SetActive(true);
            }

            void TryFace(int3 index, Vector3 center, Vector3 half, Vector2 uv, int axis, int sign)
            {
                var step = new int3();
                step[axis] = sign;
                if (_boneIds.Contains(Pack(index + step)))
                    return;

                var normal = new Vector3();
                normal[axis] = sign;
                int rightAxis = (axis + 1) % 3;
                int upAxis = (axis + 2) % 3;
                var right = new Vector3();
                var up = new Vector3();
                right[rightAxis] = half[rightAxis];
                up[upAxis] = half[upAxis];
                Vector3 origin = center + normal * half[axis];
                Vector3 a = origin - right - up;
                Vector3 b = origin + right - up;
                Vector3 c = origin + right + up;
                Vector3 d = origin - right + up;
                if (sign < 0)
                {
                    Vector3 swap = b;
                    b = d;
                    d = swap;
                }

                int start = _vertices.Count;
                _vertices.Add(a);
                _vertices.Add(b);
                _vertices.Add(c);
                _vertices.Add(d);
                _normals.Add(normal);
                _normals.Add(normal);
                _normals.Add(normal);
                _normals.Add(normal);
                _uvs.Add(uv);
                _uvs.Add(uv);
                _uvs.Add(uv);
                _uvs.Add(uv);
                _triangles.Add(start);
                _triangles.Add(start + 1);
                _triangles.Add(start + 2);
                _triangles.Add(start);
                _triangles.Add(start + 2);
                _triangles.Add(start + 3);
            }

            void HideFlesh(HumanLimb limb)
            {
                MeshRenderer[] renderers = limb.GetComponentsInChildren<MeshRenderer>(true);
                if (renderers == null)
                    return;
                for (int i = 0; i < renderers.Length; i++)
                {
                    MeshRenderer renderer = renderers[i];
                    if (renderer == null || renderer.gameObject.name == VisualName)
                        continue;
                    if (_hidden.Contains(renderer))
                    {
                        renderer.forceRenderingOff = true;
                        continue;
                    }
                    _hidden.Add(renderer);
                    _wasForcedOff.Add(renderer.forceRenderingOff);
                    _materials.Add(renderer.sharedMaterial);
                    var block = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(block);
                    _blocks.Add(block);
                    renderer.forceRenderingOff = true;
                }
            }

            static Material FleshMaterial(HumanLimb limb)
            {
                MeshRenderer[] renderers = limb.GetComponentsInChildren<MeshRenderer>(true);
                if (renderers == null)
                    return null;
                for (int i = 0; i < renderers.Length; i++)
                {
                    MeshRenderer renderer = renderers[i];
                    if (renderer == null || renderer.gameObject.name == VisualName || renderer.sharedMaterial == null)
                        continue;
                    return renderer.sharedMaterial;
                }
                return null;
            }

            public void KeepHidden()
            {
                for (int i = 0; i < _hidden.Count; i++)
                {
                    if (_hidden[i] != null)
                        _hidden[i].forceRenderingOff = true;
                }
            }

            public void Restore()
            {
                for (int i = 0; i < _hidden.Count; i++)
                {
                    MeshRenderer renderer = _hidden[i];
                    if (renderer == null)
                        continue;
                    if (_materials[i] != null)
                        renderer.sharedMaterial = _materials[i];
                    if (_blocks[i] != null && !_blocks[i].isEmpty)
                        renderer.SetPropertyBlock(_blocks[i]);
                    renderer.forceRenderingOff = _wasForcedOff[i];
                }
                _hidden.Clear();
                _wasForcedOff.Clear();
                _materials.Clear();
                _blocks.Clear();
                _enabledCount = -1;
                if (_voxelMesh != null)
                {
                    try { _voxelMesh.Show(); } catch { }
                }
                if (Limb != null)
                {
                    VoxelMeshChunk[] chunks = Limb.GetComponentsInChildren<VoxelMeshChunk>(true);
                    if (chunks != null)
                    {
                        for (int i = 0; i < chunks.Length; i++)
                        {
                            if (chunks[i] != null)
                            {
                                try { chunks[i].Show(); } catch { }
                            }
                        }
                    }
                }
                DropVisual();
            }

            void DropVisual()
            {
                if (_mesh != null)
                {
                    Object.Destroy(_mesh);
                    _mesh = null;
                }
                if (_visual != null)
                {
                    Object.Destroy(_visual);
                    _visual = null;
                }
            }

            bool Matches(AbstractOrgan organ)
            {
                return (_showBones && IsBone(organ)) || (_showOrgans && IsOrgan(organ));
            }

            static bool IsBone(AbstractOrgan organ)
            {
                return organ != null && (organ.TryCast<Bone>() != null
                    || organ.TryCast<Rib>() != null
                    || organ.TryCast<Skull>() != null
                    || organ.TryCast<Backbone>() != null);
            }

            static bool IsOrgan(AbstractOrgan organ)
            {
                return organ != null && (organ.TryCast<Brain>() != null
                    || organ.TryCast<Eye>() != null
                    || organ.TryCast<Lens>() != null
                    || organ.TryCast<Heart>() != null
                    || organ.TryCast<Lung>() != null
                    || organ.TryCast<Liver>() != null
                    || organ.TryCast<Stomach>() != null
                    || organ.TryCast<TestOrganPiska>() != null);
            }

            static Vector3 Pitch(VoxelMesh mesh)
            {
                Vector3 origin = Local(mesh, new int3(0, 0, 0));
                Vector3 x = Local(mesh, new int3(1, 0, 0));
                Vector3 y = Local(mesh, new int3(0, 1, 0));
                Vector3 z = Local(mesh, new int3(0, 0, 1));
                return new Vector3(
                    Mathf.Max(0.001f, Vector3.Distance(origin, x)),
                    Mathf.Max(0.001f, Vector3.Distance(origin, y)),
                    Mathf.Max(0.001f, Vector3.Distance(origin, z)));
            }

            static Vector3 Local(VoxelMesh mesh, int3 index)
            {
                return mesh.transform.InverseTransformPoint(VoxelTools.VoxelIndexToWorldPosition(mesh, index));
            }

            static Vector2 UV(RGBAtlasColor color)
            {
                return RGBAtlasTexture.GetUVCoordinatesForColorCorrected(color);
            }

            static int Pack(int3 index)
            {
                return index.x + (index.y << 10) + (index.z << 20);
            }
        }
    }
}
