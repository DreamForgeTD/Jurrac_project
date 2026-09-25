import bpy
import math
import os
from mathutils import Vector

ROOT = os.path.dirname(os.path.abspath(__file__))
PROJECT_ROOT = os.path.dirname(os.path.dirname(ROOT))
SOURCE_BLEND = os.path.join(ROOT, "SodaCan_330ml.blend")
WOOD_TEXTURE = os.path.join(PROJECT_ROOT, "Assets", "Models", "Targets", "CanTargetStand", "WoodStand_BaseColor.png")
ASSET_ROOT = os.path.join(PROJECT_ROOT, "Assets", "Models", "Targets", "CanTargetStand")
CAN_ASSET_ROOT = os.path.join(PROJECT_ROOT, "Assets", "Models", "Targets", "SodaCan")
CAN_TEXTURE = os.path.join(CAN_ASSET_ROOT, "SodaCan_BaseColor.png")
BLEND = os.path.join(ROOT, "CanTargetStack_12Cans.blend")
PREVIEW = os.path.join(ROOT, "CanTargetStack_Preview.png")
STAND_FBX = os.path.join(ASSET_ROOT, "WoodCanTargetStand.fbx")
CAN_FBX = os.path.join(CAN_ASSET_ROOT, "SodaCan_330ml.fbx")
os.makedirs(ASSET_ROOT, exist_ok=True)
os.makedirs(CAN_ASSET_ROOT, exist_ok=True)

# Start clean, then append only the existing low-poly can and its dependencies.
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
for datablocks in (bpy.data.meshes, bpy.data.curves, bpy.data.cameras, bpy.data.lights):
    for datablock in list(datablocks):
        if datablock.users == 0:
            datablocks.remove(datablock)

scene = bpy.context.scene
scene.unit_settings.system = 'METRIC'
scene.unit_settings.scale_length = 1.0
scene.render.engine = 'CYCLES'
scene.cycles.samples = 24
scene.render.resolution_x = 1100
scene.render.resolution_y = 760
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = 'PNG'
scene.render.film_transparent = False
scene.view_settings.view_transform = 'AgX'
scene.render.filepath = PREVIEW
scene.world = scene.world or bpy.data.worlds.new("CanTargetStack_World")
scene.world.use_nodes = True
world_bg = scene.world.node_tree.nodes.get("Background")
world_bg.inputs["Color"].default_value = (0.055, 0.062, 0.075, 1)
world_bg.inputs["Strength"].default_value = 0.35

with bpy.data.libraries.load(SOURCE_BLEND, link=False) as (data_from, data_to):
    if "SodaCan_330ml" not in data_from.objects:
        raise RuntimeError("SodaCan_330ml object was not found in the source blend")
    data_to.objects = ["SodaCan_330ml"]
can_template = data_to.objects[0]
scene.collection.objects.link(can_template)
can_template.hide_render = False
can_template.hide_set(False)
can_mesh = can_template.data
can_mesh.name = "SodaCan_330ml_Mesh"
can_image = bpy.data.images.load(CAN_TEXTURE, check_existing=False)
can_image.name = "T_SodaCan_BaseColor_1024"
can_image.colorspace_settings.name = 'sRGB'
for material in can_mesh.materials:
    if material and material.use_nodes:
        for node in material.node_tree.nodes:
            if node.type == 'TEX_IMAGE' and node.image:
                node.image = can_image

# A single low-poly mesh with a long board and two solid support blocks.
wood = bpy.data.materials.new("M_WoodCanTargetStand")
wood.use_nodes = True
wood.diffuse_color = (0.43, 0.23, 0.11, 1)
wood_bsdf = wood.node_tree.nodes.get("Principled BSDF")
wood_bsdf.inputs["Metallic"].default_value = 0.0
wood_bsdf.inputs["Roughness"].default_value = 0.68
wood_img = bpy.data.images.load(WOOD_TEXTURE, check_existing=True)
wood_img.name = "T_WoodStand_BaseColor_1024"
wood_img.colorspace_settings.name = 'sRGB'
wood_tex = wood.node_tree.nodes.new("ShaderNodeTexImage")
wood_tex.name = "Wood Base Color Atlas"
wood_tex.label = "Single 1024 x 1024 wood base-color texture"
wood_tex.image = wood_img
wood.node_tree.links.new(wood_tex.outputs["Color"], wood_bsdf.inputs["Base Color"])

wood_verts = []
wood_faces = []
wood_face_uvs = []

def add_box(center, size, grain_axis):
    cx, cy, cz = center
    sx, sy, sz = (value / 2 for value in size)
    lo = (cx - sx, cy - sy, cz - sz)
    hi = (cx + sx, cy + sy, cz + sz)
    corners = [
        (lo[0], lo[1], lo[2]), (hi[0], lo[1], lo[2]),
        (hi[0], hi[1], lo[2]), (lo[0], hi[1], lo[2]),
        (lo[0], lo[1], hi[2]), (hi[0], lo[1], hi[2]),
        (hi[0], hi[1], hi[2]), (lo[0], hi[1], hi[2]),
    ]
    base = len(wood_verts)
    wood_verts.extend(corners)
    box_faces = [
        (0, 3, 2, 1), (4, 5, 6, 7),
        (0, 1, 5, 4), (1, 2, 6, 5),
        (2, 3, 7, 6), (3, 0, 4, 7),
    ]
    for face in box_faces:
        face = tuple(base + index for index in face)
        wood_faces.append(face)
        face_axes = [axis for axis in range(3) if max(wood_verts[index][axis] for index in face) - min(wood_verts[index][axis] for index in face) > 1e-8]
        u_axis = grain_axis if grain_axis in face_axes else face_axes[0]
        v_axis = next(axis for axis in face_axes if axis != u_axis)
        uv = []
        for index in face:
            p = wood_verts[index]
            uv.append(((p[u_axis] - lo[u_axis]) / size[u_axis], (p[v_axis] - lo[v_axis]) / size[v_axis]))
        wood_face_uvs.append(uv)

BOARD_LENGTH = 0.56
BOARD_DEPTH = 0.16
BOARD_THICKNESS = 0.035
SUPPORT_WIDTH = 0.075
SUPPORT_DEPTH = 0.14
SUPPORT_HEIGHT = 0.235
BOARD_BOTTOM = SUPPORT_HEIGHT
BOARD_TOP = BOARD_BOTTOM + BOARD_THICKNESS
BOARD_CENTER_Z = BOARD_BOTTOM + BOARD_THICKNESS / 2
SUPPORT_OFFSET_X = 0.205

add_box((0, 0, BOARD_CENTER_Z), (BOARD_LENGTH, BOARD_DEPTH, BOARD_THICKNESS), grain_axis=0)
for support_x in (-SUPPORT_OFFSET_X, SUPPORT_OFFSET_X):
    add_box((support_x, 0, SUPPORT_HEIGHT / 2), (SUPPORT_WIDTH, SUPPORT_DEPTH, SUPPORT_HEIGHT), grain_axis=2)

wood_mesh = bpy.data.meshes.new("WoodCanTargetStand_Mesh")
wood_mesh.from_pydata(wood_verts, [], wood_faces)
wood_mesh.materials.append(wood)
wood_mesh.update()
uv_layer = wood_mesh.uv_layers.new(name="UVMap")
for poly, face_uvs in zip(wood_mesh.polygons, wood_face_uvs):
    poly.use_smooth = False
    for loop_index, uv in zip(poly.loop_indices, face_uvs):
        uv_layer.data[loop_index].uv = uv
stand = bpy.data.objects.new("WoodCanTargetStand", wood_mesh)
scene.collection.objects.link(stand)
stand["Dimensions"] = "560 x 160 x 270 mm"
stand["Construction"] = "One mesh with a horizontal plank and two support blocks"
stand["Material and texture"] = "One wood material with one 1024 x 1024 base-color texture"
bevel = stand.modifiers.new("Soft board edges", 'BEVEL')
bevel.width = 0.0012
bevel.segments = 1
bevel.limit_method = 'ANGLE'
bevel.angle_limit = math.radians(30)
bevel.harden_normals = True
bpy.context.view_layer.objects.active = stand
stand.select_set(True)
bpy.ops.object.modifier_apply(modifier=bevel.name)
stand.select_set(False)

# Six side-by-side columns, each two cans high. Each can remains a separate object
# while sharing the same mesh and material for efficient rendering.
can_objects = []
can_spacing = 0.070
bottom_can_z = BOARD_TOP - 0.001
second_can_z = bottom_can_z + 0.1136
for row, z in enumerate((bottom_can_z, second_can_z), start=1):
    for col in range(6):
        x = (col - 2.5) * can_spacing
        if row == 1 and col == 0:
            can = can_template
        else:
            can = can_template.copy()
            can.data = can_mesh
            scene.collection.objects.link(can)
        can.name = "SodaCan_R%02d_C%02d" % (row, col + 1)
        can.location = (x, 0, z)
        can.rotation_euler = (0, 0, 0)
        can.hide_render = False
        can.hide_set(False)
        can["Stack position"] = "Row %d, column %d" % (row, col + 1)
        can_objects.append(can)

# Preview-only floor, camera and lights live in a separate collection.
presentation = bpy.data.collections.new("Presentation (not exported)")
scene.collection.children.link(presentation)
ground_mat = bpy.data.materials.new("Preview_Ground")
ground_mat.use_nodes = True
ground_bsdf = ground_mat.node_tree.nodes.get("Principled BSDF")
ground_bsdf.inputs["Base Color"].default_value = (0.035, 0.045, 0.058, 1)
ground_bsdf.inputs["Roughness"].default_value = 0.76
bpy.ops.mesh.primitive_plane_add(size=2.0, location=(0, 0, -0.001))
ground = bpy.context.object
ground.name = "Preview Ground"
ground.data.materials.append(ground_mat)

def move_to(obj, collection):
    for old_collection in list(obj.users_collection):
        old_collection.objects.unlink(obj)
    collection.objects.link(obj)

def aim(obj, target):
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat('-Z', 'Y').to_euler()

move_to(ground, presentation)
bpy.ops.object.camera_add(location=(0, -0.84, 0.56))
camera = bpy.context.object
camera.name = "Preview Camera"
camera.data.type = 'ORTHO'
camera.data.ortho_scale = 0.84
aim(camera, (0, 0, 0.235))
move_to(camera, presentation)
scene.camera = camera

def area(name, location, power, size, color):
    bpy.ops.object.light_add(type='AREA', location=location)
    obj = bpy.context.object
    obj.name = name
    obj.data.energy = power
    obj.data.shape = 'DISK'
    obj.data.size = size
    obj.data.color = color
    aim(obj, (0, 0, 0.22))
    move_to(obj, presentation)

area("Key Light", (-0.38, -0.48, 0.82), 15, 0.52, (1.0, 0.82, 0.66))
area("Fill Light", (0.48, -0.16, 0.48), 9, 0.42, (0.70, 0.82, 1.0))
area("Rim Light", (0.02, 0.38, 0.72), 18, 0.36, (1.0, 0.38, 0.24))

# Render the complete knockdown target assembly for review.
scene.render.filepath = PREVIEW
bpy.ops.render.render(write_still=True)

# Save a clean native project, then export the stand and complete stack separately.
bpy.context.preferences.filepaths.save_version = 0
for obj in presentation.objects:
    obj.hide_set(True)
bpy.ops.object.select_all(action='DESELECT')
for obj in can_objects:
    obj.select_set(True)
stand.select_set(True)
bpy.context.view_layer.objects.active = stand
for screen in bpy.data.screens:
    for area_space in screen.areas:
        if area_space.type == 'VIEW_3D':
            area_space.spaces.active.region_3d.view_location = (0, 0, 0.25)
            area_space.spaces.active.region_3d.view_distance = 0.82
            area_space.spaces.active.shading.type = 'MATERIAL'
bpy.ops.wm.save_as_mainfile(filepath=BLEND)

fbx_options = dict(
    object_types={'MESH'}, apply_unit_scale=True, global_scale=1.0,
    axis_forward='-Z', axis_up='Y', use_mesh_modifiers=True,
    mesh_smooth_type='FACE', use_tspace=True, path_mode='RELATIVE',
    embed_textures=False, add_leaf_bones=False, bake_anim=False,
)
bpy.ops.object.select_all(action='DESELECT')
stand.select_set(True)
bpy.context.view_layer.objects.active = stand
bpy.ops.export_scene.fbx(filepath=STAND_FBX, use_selection=True, **fbx_options)

bpy.ops.object.select_all(action='DESELECT')
can_asset = can_objects[0]
stack_location = can_asset.location.copy()
can_asset.location = (0, 0, 0)
can_asset.select_set(True)
bpy.context.view_layer.objects.active = can_asset
bpy.ops.export_scene.fbx(filepath=CAN_FBX, use_selection=True, **fbx_options)
can_asset.location = stack_location

can_tris = sum(len(poly.vertices) - 2 for poly in can_mesh.polygons)
stand_tris = sum(len(poly.vertices) - 2 for poly in stand.data.polygons)
print("Created", BLEND)
print("Preview", PREVIEW)
print("Exported", STAND_FBX)
print("Exported", CAN_FBX)
print("Can mesh: %d vertices, %d polygons, %d triangles" % (len(can_mesh.vertices), len(can_mesh.polygons), can_tris))
print("Stand mesh: %d vertices, %d polygons, %d triangles" % (len(stand.data.vertices), len(stand.data.polygons), stand_tris))
print("Stack cans: %d separate objects" % len(can_objects))
