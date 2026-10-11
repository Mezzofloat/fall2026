"""
Export an APPROVED .blend to per-part FBX for Unity, triangulating faces first (Unity mis-triangulates ring-shaped n-gons such as a
bezel's front face). The .blend is opened read-only and never saved, so its review approval stays valid.
Usage:  blender -b art/<project>/out/model.blend --python scripts/export_unity.py -- <out_dir>
Matches `cad export --format fbx --per-part` (scene scaled by the unit scale, apply_unit_scale=True).
"""
import os
import sys

import bmesh
import bpy
from mathutils import Matrix

out_dir = sys.argv[sys.argv.index("--") + 1] if "--" in sys.argv else "unity-export"
os.makedirs(out_dir, exist_ok=True)
scene = bpy.context.scene
scale = scene.unit_settings.scale_length
dg = bpy.context.evaluated_depsgraph_get()

exports = []
for obj in list(scene.objects):
    if obj.type != "MESH" or obj.hide_get() or obj.get("cad_helper"):
        continue
    evaluated = obj.evaluated_get(dg)
    mesh = bpy.data.meshes.new_from_object(evaluated)
    bm = bmesh.new(); bm.from_mesh(mesh)
    bmesh.ops.triangulate(bm, faces=bm.faces[:], quad_method="BEAUTY", ngon_method="BEAUTY")
    # Box-projected UVs (1 tile per metre in design units) so Unity's weathered surface textures can map onto the props.
    uv = bm.loops.layers.uv.verify()
    for face in bm.faces:
        n = face.normal; ax = max(range(3), key=lambda i: abs(n[i]))
        for loop in face.loops:
            c = loop.vert.co
            u, v = ((c.y, c.z), (c.x, c.z), (c.x, c.y))[ax]
            loop[uv].uv = (u / 1000.0, v / 1000.0)
    bm.to_mesh(mesh); bm.free()
    name = obj.name
    obj.name = name + "_source"          # free the name so the export copy keeps it (no ".001" suffix)
    copy = bpy.data.objects.new(name, mesh)
    scene.collection.objects.link(copy)
    copy.matrix_world = Matrix.Scale(scale, 4) @ obj.matrix_world
    obj.hide_set(True)
    exports.append(copy)
bpy.context.view_layer.update()

for obj in exports:
    for o in scene.objects:
        o.select_set(o is obj)
    bpy.context.view_layer.objects.active = obj
    path = os.path.join(out_dir, obj.name + ".fbx")
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, apply_unit_scale=True)
    print("UNITY_EXPORT", path)
