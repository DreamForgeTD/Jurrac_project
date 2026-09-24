"""Build an editable, low-poly toy cannon for a casual puzzle game.

This script only creates a Blender project and a PNG preview. It deliberately
does not export anything to the Unity project.
"""

import math
from pathlib import Path

import bmesh
import bpy
from mathutils import Vector


OUTPUT_DIR = Path(__file__).resolve().parent
BLEND_PATH = OUTPUT_DIR / "PuzzleCannon.blend"
PREVIEW_PATH = OUTPUT_DIR / "PuzzleCannon_preview.png"


def srgb_channel(value):
    value /= 255.0
    return value / 12.92 if value <= 0.04045 else ((value + 0.055) / 1.055) ** 2.4


def make_material(name, color_hex, roughness=0.48, metallic=0.0, emission=0.0):
    rgb = tuple(srgb_channel(int(color_hex[index:index + 2], 16)) for index in (0, 2, 4))
    material = bpy.data.materials.new(name)
    material.diffuse_color = (*rgb, 1.0)
    material.use_nodes = True
    shader = material.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = (*rgb, 1.0)
    shader.inputs["Roughness"].default_value = roughness
    shader.inputs["Metallic"].default_value = metallic
    if emission:
        if "Emission Color" in shader.inputs:
            shader.inputs["Emission Color"].default_value = (*rgb, 1.0)
            shader.inputs["Emission Strength"].default_value = emission
        else:
            shader.inputs["Emission"].default_value = (*rgb, 1.0)
            shader.inputs["Emission Strength"].default_value = emission
    return material


def move_to_collection(obj, collection):
    for old_collection in list(obj.users_collection):
        old_collection.objects.unlink(obj)
    collection.objects.link(obj)
    return obj


def finish_mesh(obj, bevel=0.0, smooth=True):
    if bevel > 0:
        modifier = obj.modifiers.new("Small toy-safe edge bevel", "BEVEL")
        modifier.width = bevel
        modifier.segments = 2
        modifier.limit_method = "ANGLE"
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    if smooth:
        for polygon in obj.data.polygons:
            polygon.use_smooth = True
        modifier = obj.modifiers.new("Weighted corner normals", "WEIGHTED_NORMAL")
        modifier.keep_sharp = True
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    return obj


def add_box(name, dimensions, location, material, collection, bevel=0.05):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=location)
    obj = bpy.context.object
    obj.name = name
    obj.data.name = f"{name} mesh"
    obj.dimensions = dimensions
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(material)
    move_to_collection(obj, collection)
    return finish_mesh(obj, bevel=bevel, smooth=True)


def add_cylinder(name, radius, depth, location, axis, material, collection, vertices=16, bevel=0.012):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth, location=location)
    obj = bpy.context.object
    obj.name = name
    obj.data.name = f"{name} mesh"
    if axis == "X":
        obj.rotation_euler[1] = math.pi / 2
    elif axis == "Y":
        obj.rotation_euler[0] = math.pi / 2
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    obj.data.materials.append(material)
    move_to_collection(obj, collection)
    return finish_mesh(obj, bevel=bevel, smooth=True)


def add_uv_sphere(name, radius, location, material, collection, segments=12, rings=8, scale=None):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, radius=radius, location=location)
    obj = bpy.context.object
    obj.name = name
    obj.data.name = f"{name} mesh"
    if scale:
        obj.scale = scale
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(material)
    move_to_collection(obj, collection)
    return finish_mesh(obj, smooth=True)


def add_torus(name, major_radius, minor_radius, location, axis, material, collection):
    bpy.ops.mesh.primitive_torus_add(
        major_segments=16,
        minor_segments=6,
        major_radius=major_radius,
        minor_radius=minor_radius,
        location=location,
    )
    obj = bpy.context.object
    obj.name = name
    obj.data.name = f"{name} mesh"
    if axis == "X":
        obj.rotation_euler[1] = math.pi / 2
    elif axis == "Y":
        obj.rotation_euler[0] = math.pi / 2
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    obj.data.materials.append(material)
    move_to_collection(obj, collection)
    return finish_mesh(obj, smooth=True)


def add_barrel(collection, materials, center_z=0.98):
    coral, cream, navy = materials
    radial_sides = 16
    specs = (
        (-0.60, 0.185),
        (-0.46, 0.225),
        (0.30, 0.225),
        (0.47, 0.285),
        (0.62, 0.300),
        (0.69, 0.300),
        (0.69, 0.215),
        (0.43, 0.178),
    )
    vertices = []
    rings = []
    for y, radius in specs:
        ring = []
        for side in range(radial_sides):
            angle = 2.0 * math.pi * side / radial_sides
            ring.append(len(vertices))
            vertices.append((radius * math.cos(angle), y, center_z + radius * math.sin(angle)))
        rings.append(ring)

    faces = []
    face_materials = []
    for ring_index in range(len(rings) - 1):
        for side in range(radial_sides):
            nxt = (side + 1) % radial_sides
            faces.append((rings[ring_index][side], rings[ring_index + 1][side],
                          rings[ring_index + 1][nxt], rings[ring_index][nxt]))
            if ring_index < 5:
                face_materials.append(0)
            elif ring_index == 5:
                face_materials.append(1)
            else:
                face_materials.append(2)

    # A visible, recessed dark bottom makes the muzzle read as a hollow tube.
    center_index = len(vertices)
    vertices.append((0.0, specs[-1][0], center_z))
    faces.append(tuple([center_index] + list(reversed(rings[-1]))))
    face_materials.append(2)

    mesh = bpy.data.meshes.new("Barrel | low-poly hollow tube mesh")
    mesh.from_pydata(vertices, [], faces)
    for material in (coral, cream, navy):
        mesh.materials.append(material)
    for polygon, material_index in zip(mesh.polygons, face_materials):
        polygon.material_index = material_index
        polygon.use_smooth = len(polygon.vertices) == 4 and material_index != 1
    mesh.update()
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(mesh)
    bm.free()

    barrel = bpy.data.objects.new("Barrel | flared hollow muzzle", mesh)
    collection.objects.link(barrel)
    return barrel


def add_triangle_pointer(name, center, material, collection, size=0.09):
    # A small raised pointer in the side plane (Y/Z), facing the camera at +X.
    x, y, z = center
    vertices = [
        (x, y, z + size),
        (x, y - size * 0.72, z - size * 0.55),
        (x, y + size * 0.72, z - size * 0.55),
    ]
    mesh = bpy.data.meshes.new(f"{name} mesh")
    mesh.from_pydata(vertices, [], [(0, 1, 2)])
    mesh.materials.append(material)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    collection.objects.link(obj)
    return obj


def make_world_and_studio(scene, studio_collection, materials):
    ground_material, = materials
    ground_material.use_nodes = True
    shader = ground_material.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = ground_material.diffuse_color
    shader.inputs["Roughness"].default_value = 0.82

    bpy.ops.mesh.primitive_plane_add(size=200.0, location=(0.0, 0.0, -0.018))
    ground = move_to_collection(bpy.context.object, studio_collection)
    ground.name = "STUDIO | Ground (render only)"
    ground.data.name = "STUDIO | Ground mesh"
    ground.data.materials.append(ground_material)

    camera_data = bpy.data.cameras.new("STUDIO | Camera")
    camera = bpy.data.objects.new("STUDIO | Camera", camera_data)
    studio_collection.objects.link(camera)
    camera.location = (3.15, 4.85, 2.75)
    target = Vector((0.0, 0.08, 0.66))
    camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = 2.7
    scene.camera = camera

    for name, location, energy, size in (
        ("STUDIO | Key", (-3.0, -2.5, 4.5), 470.0, 3.8),
        ("STUDIO | Fill", (3.5, 1.6, 3.0), 290.0, 3.4),
        ("STUDIO | Rim", (-0.5, 4.0, 3.6), 360.0, 2.8),
    ):
        data = bpy.data.lights.new(name, "AREA")
        data.energy = energy
        data.shape = "DISK"
        data.size = size
        light = bpy.data.objects.new(name, data)
        studio_collection.objects.link(light)
        light.location = location
        light.rotation_euler = (target - light.location).to_track_quat("-Z", "Y").to_euler()

    world = bpy.data.worlds.new("STUDIO | Soft pastel world")
    scene.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.69, 0.81, 0.80, 1.0)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.65


def configure_scene(model_collection):
    scene = bpy.context.scene
    available_engines = {item.identifier for item in scene.render.bl_rna.properties["engine"].enum_items}
    scene.render.engine = "BLENDER_EEVEE_NEXT" if "BLENDER_EEVEE_NEXT" in available_engines else "BLENDER_EEVEE"
    scene.render.resolution_x = 1080
    scene.render.resolution_y = 810
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGB"
    scene.render.filepath = str(PREVIEW_PATH)
    scene.render.film_transparent = False
    scene.view_settings.view_transform = "AgX"
    scene.view_settings.look = "AgX - Medium High Contrast"
    scene.camera.data.lens = 50
    scene.render.image_settings.color_depth = "8"
    scene.world.color = (0.7, 0.8, 0.8)

    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type == "VIEW_3D":
                space = area.spaces.active
                space.region_3d.view_perspective = "ORTHO"
                space.region_3d.view_location = Vector((0.0, 0.10, 0.65))
                space.region_3d.view_distance = 3.0
                view_direction = (Vector((3.15, 4.85, 2.75)) - Vector((0.0, 0.08, 0.66))).to_track_quat("-Z", "Y")
                space.region_3d.view_rotation = view_direction
                space.shading.type = "MATERIAL"
                if hasattr(space.overlay, "show_floor"):
                    space.overlay.show_floor = False
                if hasattr(space.overlay, "show_extras"):
                    space.overlay.show_extras = False
            elif area.type == "OUTLINER":
                pass

    bpy.ops.object.select_all(action="DESELECT")
    for obj in model_collection.objects:
        if obj.type == "MESH":
            obj.select_set(True)
    barrel = bpy.data.objects.get("Barrel | flared hollow muzzle")
    if barrel:
        bpy.context.view_layer.objects.active = barrel


def build_cannon():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    bpy.context.preferences.filepaths.save_version = 0

    model_collection = bpy.data.collections.new("PUZZLE CANNON | Editable Model")
    scene.collection.children.link(model_collection)
    studio_collection = bpy.data.collections.new("STUDIO | Preview only")
    scene.collection.children.link(studio_collection)

    mint = make_material("Paint | lagoon mint", "54C7B5", roughness=0.4)
    deep_teal = make_material("Paint | deep teal", "236F78", roughness=0.42)
    coral = make_material("Paint | coral red", "F17769", roughness=0.39)
    cream = make_material("Paint | warm cream", "FFF0D8", roughness=0.43)
    gold = make_material("Trim | soft brass", "F3B94F", roughness=0.31, metallic=0.42)
    navy = make_material("Bore | midnight blue", "233443", roughness=0.38)
    cyan = make_material("Puzzle light | aqua", "65DCE0", roughness=0.27, emission=0.12)
    pink = make_material("Puzzle light | pink", "F28AB0", roughness=0.3, emission=0.08)
    yellow = make_material("Puzzle light | lemon", "FFE16B", roughness=0.3, emission=0.08)

    # Low, rounded carriage with two contrasting skids.
    add_box("Carriage | rounded mint body", (0.77, 1.10, 0.27), (0.0, -0.03, 0.245), mint, model_collection, 0.10)
    add_box("Carriage | top saddle", (0.59, 0.70, 0.18), (0.0, 0.10, 0.445), deep_teal, model_collection, 0.075)
    add_box("Runner | near side", (0.10, 0.94, 0.09), (0.0, -0.03, 0.075), gold, model_collection, 0.038)
    add_box("Runner | far side", (0.10, 0.94, 0.09), (0.0, -0.03, 0.075), gold, model_collection, 0.038)
    # Separate decorative runner rails are positioned just outside the central keel.
    bpy.data.objects["Runner | near side"].location.x = 0.255
    bpy.data.objects["Runner | far side"].location.x = -0.255

    # Side wheels, low-poly and independently selectable.
    wheel_y, wheel_z = -0.10, 0.365
    for side, sign in (("near", 1), ("far", -1)):
        wheel_x = sign * 0.475
        add_cylinder(f"Wheel | {side} teal tire", 0.345, 0.17, (wheel_x, wheel_y, wheel_z), "X", deep_teal, model_collection, vertices=18, bevel=0.025)
        face_x = sign * 0.575
        add_cylinder(f"Wheel | {side} cream rim", 0.255, 0.045, (face_x, wheel_y, wheel_z), "X", cream, model_collection, vertices=18, bevel=0.012)
        add_torus(f"Wheel | {side} brass pinstripe", 0.224, 0.018, (sign * 0.603, wheel_y, wheel_z), "X", gold, model_collection)
        add_cylinder(f"Hub | {side} brass puzzle dial", 0.155, 0.055, (sign * 0.615, wheel_y, wheel_z), "X", gold, model_collection, vertices=16, bevel=0.01)
        add_cylinder(f"Hub | {side} cream inset", 0.116, 0.024, (sign * 0.651, wheel_y, wheel_z), "X", cream, model_collection, vertices=16, bevel=0.008)
        add_cylinder(f"Hub | {side} center cap", 0.050, 0.035, (sign * 0.669, wheel_y, wheel_z), "X", mint, model_collection, vertices=12, bevel=0.008)

    # Pivot/support and the main horizontal barrel. Forward direction is local +Y.
    add_box("Trunnion | near bearing", (0.17, 0.28, 0.27), (0.34, -0.06, 0.64), gold, model_collection, 0.055)
    add_box("Trunnion | far bearing", (0.17, 0.28, 0.27), (-0.34, -0.06, 0.64), gold, model_collection, 0.055)
    add_cylinder("Barrel | elevation pivot axle", 0.095, 0.83, (0.0, -0.06, 0.77), "X", deep_teal, model_collection, vertices=16, bevel=0.012)
    add_box("Cradle | padded barrel rest", (0.53, 0.54, 0.18), (0.0, 0.04, 0.65), cream, model_collection, 0.075)

    barrel_center_z = 0.925
    barrel = add_barrel(model_collection, (coral, cream, navy), center_z=barrel_center_z)
    # Colored reinforcement collars echo a playful toy design and remain separate.
    add_torus("Barrel band | breech brass", 0.232, 0.028, (0.0, -0.31, barrel_center_z), "Y", gold, model_collection)
    add_torus("Barrel band | muzzle brass", 0.242, 0.024, (0.0, 0.26, barrel_center_z), "Y", gold, model_collection)
    add_cylinder("Breech | rounded rear cap", 0.19, 0.10, (0.0, -0.62, barrel_center_z), "Y", deep_teal, model_collection, vertices=16, bevel=0.035)
    add_uv_sphere("Breech | cream end button", 0.07, (0.0, -0.682, barrel_center_z), cream, model_collection, scale=(1.0, 0.45, 1.0))

    # Aim bead sights; their restrained size leaves the flared muzzle as the focal point.
    add_box("Sight | rear post", (0.075, 0.085, 0.13), (0.0, -0.30, 1.195), deep_teal, model_collection, 0.025)
    add_uv_sphere("Sight | front bead", 0.055, (0.0, 0.48, 1.235), yellow, model_collection, segments=12, rings=8)

    # Three colored lock lights plus a pointer turn the near wheel hub into a readable puzzle dial.
    dial_x = 0.691
    indicator_specs = (
        ("Aqua", -0.078, 0.430, cyan),
        ("Lemon", 0.0, 0.455, yellow),
        ("Pink", 0.078, 0.430, pink),
    )
    for label, offset_y, offset_z, material in indicator_specs:
        add_uv_sphere(
            f"Puzzle dial | {label} lock light",
            0.025,
            (dial_x, wheel_y + offset_y, wheel_z + offset_z - wheel_z),
            material,
            model_collection,
            segments=10,
            rings=6,
        )
    add_triangle_pointer("Puzzle dial | coral selector", (0.722, wheel_y, wheel_z + 0.015), coral, model_collection, size=0.072)
    add_cylinder("Puzzle dial | selector pivot", 0.028, 0.025, (0.702, wheel_y, wheel_z), "X", coral, model_collection, vertices=12, bevel=0.005)

    # A little trigger/charge lever gives the rear of the cannon a tactile puzzle cue.
    add_cylinder("Trigger | gold stem", 0.036, 0.23, (0.0, -0.36, 0.46), "X", gold, model_collection, vertices=12, bevel=0.01)
    add_uv_sphere("Trigger | coral knob", 0.075, (0.0, -0.36, 0.34), coral, model_collection, segments=12, rings=8, scale=(1.0, 0.85, 0.72))

    # A small, smooth star badge on the carriage side; geometry is baked into one light mesh.
    badge = add_cylinder("Badge | brass star backing", 0.105, 0.035, (0.397, -0.405, 0.245), "X", gold, model_collection, vertices=16, bevel=0.008)
    badge_star_verts = []
    for index in range(10):
        angle = math.pi * 0.5 + index * math.pi / 5.0
        radius = 0.075 if index % 2 == 0 else 0.035
        badge_star_verts.append((0.421, -0.405 + radius * math.cos(angle), 0.245 + radius * math.sin(angle)))
    star_mesh = bpy.data.meshes.new("Badge | star relief mesh")
    star_mesh.from_pydata(badge_star_verts, [], [tuple(range(10))])
    star_mesh.materials.append(cream)
    star_mesh.update()
    star = bpy.data.objects.new("Badge | cream star", star_mesh)
    model_collection.objects.link(star)

    # Keep the scene easy to edit: join small details into six logical assemblies.
    def join_parts(new_name, member_names, active_name):
        members = [bpy.data.objects[name] for name in member_names]
        bpy.ops.object.select_all(action="DESELECT")
        for member in members:
            member.select_set(True)
        active = bpy.data.objects[active_name]
        bpy.context.view_layer.objects.active = active
        bpy.ops.object.join()
        active.name = new_name
        active.data.name = f"{new_name} mesh"
        return active

    join_parts(
        "Carriage | body and trim",
        [
            "Carriage | rounded mint body", "Carriage | top saddle",
            "Runner | near side", "Runner | far side",
            "Badge | brass star backing", "Badge | cream star",
        ],
        "Carriage | rounded mint body",
    )
    join_parts(
        "Wheel | near side with puzzle dial",
        [
            "Wheel | near teal tire", "Wheel | near cream rim", "Wheel | near brass pinstripe",
            "Hub | near brass puzzle dial", "Hub | near cream inset", "Hub | near center cap",
            "Puzzle dial | Aqua lock light", "Puzzle dial | Lemon lock light", "Puzzle dial | Pink lock light",
            "Puzzle dial | coral selector", "Puzzle dial | selector pivot",
        ],
        "Wheel | near teal tire",
    )
    join_parts(
        "Wheel | far side",
        [
            "Wheel | far teal tire", "Wheel | far cream rim", "Wheel | far brass pinstripe",
            "Hub | far brass puzzle dial", "Hub | far cream inset", "Hub | far center cap",
        ],
        "Wheel | far teal tire",
    )
    join_parts(
        "Barrel | complete hollow cannon assembly",
        [
            "Barrel | flared hollow muzzle", "Barrel band | breech brass", "Barrel band | muzzle brass",
            "Breech | rounded rear cap", "Breech | cream end button",
            "Sight | rear post", "Sight | front bead",
        ],
        "Barrel | flared hollow muzzle",
    )
    join_parts(
        "Elevation | cradle and bearings",
        [
            "Trunnion | near bearing", "Trunnion | far bearing",
            "Barrel | elevation pivot axle", "Cradle | padded barrel rest",
        ],
        "Cradle | padded barrel rest",
    )
    join_parts(
        "Trigger | charge lever",
        ["Trigger | gold stem", "Trigger | coral knob"],
        "Trigger | gold stem",
    )

    floor_mat = make_material("STUDIO | warm ivory backdrop", "E8F0E9", roughness=0.82)
    make_world_and_studio(scene, studio_collection, (floor_mat,))
    configure_scene(model_collection)

    # The root collection stays easy to find; the preview rig can be hidden in the viewport.
    studio_collection.hide_viewport = True
    studio_collection.hide_render = False
    bpy.ops.render.render(write_still=True)

    studio_collection.hide_viewport = True
    bpy.ops.object.select_all(action="DESELECT")
    for obj in model_collection.objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = barrel
    bpy.ops.file.pack_all()
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_PATH))

    model_meshes = [obj for obj in model_collection.objects if obj.type == "MESH"]
    vertices = sum(len(obj.data.vertices) for obj in model_meshes)
    triangles = sum(sum(len(poly.vertices) - 2 for poly in obj.data.polygons) for obj in model_meshes)
    print(f"Puzzle cannon created: {len(model_meshes)} separate mesh parts, {vertices} vertices, {triangles} triangles")
    print(f"Blend: {BLEND_PATH}")
    print(f"Preview: {PREVIEW_PATH}")
    print("No Unity files were written or exported.")


if __name__ == "__main__":
    build_cannon()
