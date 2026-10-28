"""Read-only audit. Run with Blender --background --python, not system Python."""
import bpy
import json
import math
import pathlib
from mathutils import Vector

ROOT = pathlib.Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'SourceArt/FlappyBird/modelo.blend'
bpy.ops.wm.open_mainfile(filepath=str(SOURCE), use_scripts=False)
character = bpy.data.objects.get('Personagem_Root')
assert character is not None, 'Character root not found; no automatic mesh selection.'
objects = [character, *character.children_recursive]
meshes = [o for o in objects if o.type == 'MESH']
materials = sorted({m for o in meshes for m in o.data.materials if m}, key=lambda m: m.name)
points = [o.matrix_world @ Vector(c) for o in meshes for c in o.bound_box]
report = {
    'blender_version': bpy.app.version_string, 'source': str(SOURCE),
    'axis_notes': '+X beak/front, +Y left, +Z up (source documentation). Unity orientation is verified after import.',
    'unit_system': bpy.context.scene.unit_settings.system,
    'unit_scale': bpy.context.scene.unit_settings.scale_length,
    'bounds_min': [min(p[a] for p in points) for a in range(3)],
    'bounds_max': [max(p[a] for p in points) for a in range(3)],
    'mesh_count': len(meshes),
    'armatures': [o.name for o in objects if o.type == 'ARMATURE'],
    'objects': [], 'materials': [], 'images': [],
}
for obj in objects:
    entry = {'name': obj.name, 'type': obj.type, 'parent': obj.parent.name if obj.parent else None,
             'location': list(obj.location), 'rotation': list(obj.rotation_euler), 'scale': list(obj.scale),
             'modifiers': [{'name': m.name, 'type': m.type} for m in obj.modifiers]}
    if obj.type == 'MESH':
        mesh = obj.data
        mesh.calc_loop_triangles()
        entry.update(vertices=len(mesh.vertices), triangles=len(mesh.loop_triangles),
                     uv_layers=[uv.name for uv in mesh.uv_layers],
                     shape_keys=[k.name for k in mesh.shape_keys.key_blocks] if mesh.shape_keys else [],
                     materials=[m.name if m else None for m in mesh.materials],
                     nonfinite_vertices=sum(not all(math.isfinite(v) for v in vert.co) for vert in mesh.vertices))
    report['objects'].append(entry)
for material in materials:
    nodes = list(material.node_tree.nodes) if material.use_nodes else []
    principled = next((node for node in nodes if node.type == 'BSDF_PRINCIPLED'), None)
    images = [node.image.name for node in nodes if node.type == 'TEX_IMAGE' and node.image]
    report['materials'].append({
        'name': material.name, 'diffuse_color': list(material.diffuse_color),
        'base_color': list(principled.inputs['Base Color'].default_value) if principled else list(material.diffuse_color),
        'roughness': float(principled.inputs['Roughness'].default_value) if principled else .7,
        'metallic': float(principled.inputs['Metallic'].default_value) if principled else 0,
        'node_types': [node.type for node in nodes], 'image_textures': images,
        'linked_inputs': [socket.name for socket in principled.inputs if socket.is_linked] if principled else [],
    })
for image in bpy.data.images:
    report['images'].append({'name': image.name, 'path': image.filepath, 'packed': bool(image.packed_file),
                             'size': list(image.size), 'used_by_character_material': any(
                                 image.name in m['image_textures'] for m in report['materials'])})
try:
    report['fbx_export_properties'] = list(bpy.ops.export_scene.fbx.get_rna_type().properties.keys())
except Exception as error:
    report['fbx_operator_error'] = str(error)
(ROOT / 'Logs/FlappyBird-model-inspection.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps({k: report[k] for k in ('mesh_count', 'armatures', 'bounds_min', 'bounds_max', 'materials', 'images')}, ensure_ascii=False))
