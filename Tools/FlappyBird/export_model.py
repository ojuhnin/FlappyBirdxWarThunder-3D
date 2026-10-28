"""Non-destructive Blender -> FBX pipeline, with a real FBX round trip."""
import bpy
import bmesh
import hashlib
import json
import pathlib
from mathutils import Vector

ROOT = pathlib.Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'SourceArt/FlappyBird/modelo.blend'
ART = ROOT / 'Assets/Art/FlappyBird'
OUTPUT = ART / 'Models/FlappyBird.fbx'

def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def mesh_summary(objects):
    result = {}
    for obj in objects:
        if obj.type != 'MESH':
            continue
        obj.data.calc_loop_triangles()
        bm = bmesh.new()
        bm.from_mesh(obj.data)
        result[obj.name] = {
            'vertices': len(obj.data.vertices), 'triangles': len(obj.data.loop_triangles),
            'uv_layers': len(obj.data.uv_layers),
            'shape_keys': len(obj.data.shape_keys.key_blocks) - 1 if obj.data.shape_keys else 0,
            'closed': all(edge.is_manifold for edge in bm.edges), 'signed_volume': bm.calc_volume(signed=True),
        }
        bm.free()
    return result

before_hash = digest(SOURCE)
bpy.ops.wm.open_mainfile(filepath=str(SOURCE), use_scripts=False)
character = bpy.data.objects.get('Personagem_Root')
assert character, 'Expected authoring root missing; refusing to select unrelated objects.'
objects = [character, *character.children_recursive]
original = mesh_summary(objects)
assert original and all(m['uv_layers'] > 0 and m['closed'] and m['signed_volume'] > 0 for m in original.values())
assert not any(obj.modifiers for obj in objects), 'Review modifiers explicitly before export; do not apply them blindly.'
for directory in ('Models', 'Materials', 'Textures/References'):
    (ART / directory).mkdir(parents=True, exist_ok=True)
audit = json.loads((ROOT / 'Logs/FlappyBird-model-inspection.json').read_text(encoding='utf-8'))
(ART / 'Materials/SourcePalette.json').write_text(json.dumps({'materials': audit['materials']}, ensure_ascii=False, indent=2), encoding='utf-8')
extracted = []
for image in bpy.data.images:
    if not image.packed_file:
        continue
    path = ART / 'Textures/References' / pathlib.PureWindowsPath(image.name).name
    path.write_bytes(bytes(image.packed_file.data))
    extracted.append(str(path.relative_to(ROOT)))

bpy.ops.object.select_all(action='DESELECT')
for obj in objects:
    obj.hide_set(False)
    obj.select_set(True)
bpy.context.view_layer.objects.active = character
result = bpy.ops.export_scene.fbx(
    filepath=str(OUTPUT), use_selection=True, object_types={'MESH', 'EMPTY', 'ARMATURE'},
    global_scale=1, apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS',
    axis_forward='-Z', axis_up='Y', bake_space_transform=False,
    use_mesh_modifiers=False, mesh_smooth_type='OFF', use_tspace=False,
    use_custom_props=True, add_leaf_bones=False, bake_anim=False,
    path_mode='AUTO', embed_textures=False, use_triangles=False,
)
assert result == {'FINISHED'} and OUTPUT.stat().st_size > 1000
bpy.ops.wm.read_factory_settings(use_empty=True)
assert bpy.ops.import_scene.fbx(filepath=str(OUTPUT), use_anim=False) == {'FINISHED'}
reimported = mesh_summary(list(bpy.context.scene.objects))
assert set(reimported) == set(original), 'Export must not join, rename, or drop character meshes.'
for name, expected in original.items():
    actual = reimported[name]
    assert actual['triangles'] == expected['triangles'], (name, 'triangle topology changed')
    assert actual['uv_layers'] == expected['uv_layers'], (name, 'UV layer lost')
    assert actual['shape_keys'] == expected['shape_keys'], (name, 'blend shape lost')
    assert actual['closed'] and actual['signed_volume'] > 0, (name, 'invalid winding or open mesh')
assert digest(SOURCE) == before_hash, 'Original source must remain byte-for-byte unchanged.'
report = {'result': 'PASS', 'blender': bpy.app.version_string, 'source_sha256': before_hash,
          'fbx_sha256': digest(OUTPUT), 'mesh_count': len(original),
          'triangles': sum(m['triangles'] for m in original.values()),
          'blend_shapes': sum(m['shape_keys'] for m in original.values()),
          'round_trip': 'Names, UV layer counts, triangles, blend shapes, closed meshes and outward winding preserved.',
          'images_extracted': extracted, 'meshes': original,
          'limitations': ['No armature in source.', 'Shape-key drivers are Blender-only; Unity blend-shape weights are not driven automatically.',
                          'Box-projected overlapping UVs are not a unique atlas.',
                          'Reference images are packed art references, not character surface textures.',
                          'Solid Principled colors/roughness are approximated by URP Lit; lighting/color management differ.']}
(ROOT / 'Logs/FlappyBird-export-validation.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
print('PASS: FBX export/real reimport:', len(original), 'separate meshes,', report['triangles'], 'triangles,', report['blend_shapes'], 'blend shapes.')
