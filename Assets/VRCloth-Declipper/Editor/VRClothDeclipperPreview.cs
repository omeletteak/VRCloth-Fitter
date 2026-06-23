#nullable enable

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using nadena.dev.ndmf.preview;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VRClothDeclipper
{
    /// <summary>
    /// Live, non-destructive preview of the fit. Shows in the Scene/Game view the
    /// exact mesh the NDMF build pass (<see cref="VRClothDeclipperPass"/>) bakes at
    /// upload — both call <see cref="VRClothPipeline.SolveToFittedMeshes"/> — so
    /// "what you see is what ships", with nothing written into the scene: the fit
    /// is applied only to NDMF's throwaway proxy renderers (No Cache holds even
    /// more strictly than the old edit-time bake). Bound to the pass via
    /// PreviewingWith(...) in <see cref="VRClothDeclipperNdmfPlugin"/>.
    /// </summary>
    internal class VRClothDeclipperPreview : IRenderFilter
    {
        public ImmutableList<RenderGroup> GetTargetGroups(ComputeContext context)
        {
            var groups = new List<RenderGroup>();
            foreach (var root in context.GetAvatarRoots())
            {
                if (context.ActiveInHierarchy(root) is not true) continue;

                foreach (var fitter in context.GetComponentsInChildren<VRClothDeclipper>(root, true))
                {
                    if (fitter == null || fitter.targetAvatar == null) continue;

                    GameObject clothRoot = fitter.clothRoot != null ? fitter.clothRoot : fitter.gameObject;
                    var renderers = context.GetComponentsInChildren<SkinnedMeshRenderer>(clothRoot, true)
                        .Where(r => r != null && r.sharedMesh != null)
                        .Cast<Renderer>()
                        .ToList();
                    if (renderers.Count == 0) continue;

                    // One group per fitter (its cloth renderers share one body
                    // proxy build); the fitter rides along as the group's data.
                    groups.Add(RenderGroup.For(renderers).WithData(fitter));
                }
            }
            return groups.ToImmutableList();
        }

        public Task<IRenderFilterNode> Instantiate(RenderGroup group,
            IEnumerable<(Renderer, Renderer)> proxyPairs, ComputeContext context)
        {
            var fitter = group.GetData<VRClothDeclipper>();
            var proxyToMesh = new Dictionary<Renderer, Mesh>();

            if (fitter != null && fitter.targetAvatar != null)
            {
                // Recompute the preview when any result-affecting setting changes.
                context.Observe(fitter, f => (f.margin, f.mode, f.forceApplyOutOfRange,
                    f.useMeshSdfCollider, f.useProjectedSolver, f.estimateRadiiFromBody, f.radiusPercentile));

                // Same shared core as the build pass → preview == upload.
                var fitted = VRClothPipeline.SolveToFittedMeshes(fitter, fitter.targetAvatar);
                var bound = new HashSet<Mesh>();
                foreach (var (original, proxy) in proxyPairs)
                {
                    var match = fitted.FirstOrDefault(f => f.renderer == original);
                    if (match.fitted != null)
                    {
                        proxyToMesh[proxy] = match.fitted;
                        bound.Add(match.fitted);
                    }
                }
                // Don't leak fitted meshes that found no matching proxy.
                foreach (var f in fitted)
                {
                    if (f.fitted != null && !bound.Contains(f.fitted))
                    {
                        Object.DestroyImmediate(f.fitted);
                    }
                }
            }

            return Task.FromResult<IRenderFilterNode>(new Node(proxyToMesh));
        }

        private class Node : IRenderFilterNode
        {
            private readonly Dictionary<Renderer, Mesh> _proxyToMesh;

            public Node(Dictionary<Renderer, Mesh> proxyToMesh)
            {
                _proxyToMesh = proxyToMesh;
            }

            public RenderAspects WhatChanged => RenderAspects.Mesh;

            public Task<IRenderFilterNode> Refresh(IEnumerable<(Renderer, Renderer)> proxyPairs,
                ComputeContext context, RenderAspects updatedAspects)
            {
                // Proxies are recreated with fresh instances; the per-proxy mesh
                // map would be stale, so rebuild cleanly via Instantiate.
                return Task.FromResult<IRenderFilterNode>(null!);
            }

            public void OnFrame(Renderer original, Renderer proxy)
            {
                if (_proxyToMesh.TryGetValue(proxy, out var mesh) && mesh != null
                    && proxy is SkinnedMeshRenderer smr)
                {
                    smr.sharedMesh = mesh;
                }
            }

            public void Dispose()
            {
                foreach (var mesh in _proxyToMesh.Values)
                {
                    if (mesh != null) Object.DestroyImmediate(mesh);
                }
                _proxyToMesh.Clear();
            }
        }
    }
}
