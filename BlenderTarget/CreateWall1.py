import math
from pathlib import Path

import bpy
from mathutils import Vector


OUTPUT_DIR = Path(__file__).resolve().parent
BLEND_PATH = OUTPUT_DIR / "Wall_1.blend"
PREVIEW_PATH = OUTPUT_DIR / "Wall_1_preview.png"
FBX_PATH = OUTPUT_DIR.parent / "Assets" / "Project" / "Resoruce_game" / "Model" / "Wall_1.fbx"


def make_material(name, color, roughness=0.45, metallic=0.0):
    material = bpy.data.materials.new(name)
    material.diffuse_color = (*color, 1.0)
    material.use_nodes = True
    shader = material.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = (*color, 1.0)
    shader.inputs["Roughness"].default_value = roughness
    shader.inputs["Metallic"].default_value = metallic
    return material


def prepare_active(obj):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def apply_modifier(obj, modifier):
    prepare_active(obj)
    bpy.ops.object.modifier_apply(modifier=modifier.name)


def move_to_collection(obj, collection):
    for current in list(obj.users_collection):
        current.objects.unlink(obj)
    collection.objects.link(obj)


def add_rounded_box(name, dimensions, location, material, bevel_width, bevel_segments=3, rotation=(0.0, 0.0, 0.0)):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = dimensions
    prepare_active(obj)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(material)

    bevel = obj.modifiers.new("Rounded corners", "BEVEL")
    bevel.width = bevel_width
    bevel.segments = bevel_segments
    bevel.limit_method = "ANGLE"
    apply_modifier(obj, bevel)
    return obj


def add_front_cylinder(name, radius, depth, location, material, vertices=48, bevel_width=0.0):
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=vertices,
        radius=radius,
        depth=depth,
        location=location,
        rotation=(math.pi / 2.0, 0.0, 0.0),
    )
    obj = bpy.context.object
    obj.name = name
    obj.data.materials.append(material)
    for polygon in obj.data.polygons:
        polygon.use_smooth = len(polygon.vertices) == 4
    if bevel_width > 0.0:
        bevel = obj.modifiers.new("Soft disk edge", "BEVEL")
        bevel.width = bevel_width
        bevel.segments = 2
        apply_modifier(obj, bevel)
    return obj


def add_front_torus(name, radius, tube, location, material, major_segments=32, minor_segments=8):
    bpy.ops.mesh.primitive_torus_add(
        major_segments=major_segments,
        minor_segments=minor_segments,
        major_radius=radius,
        minor_radius=tube,
        location=location,
        rotation=(math.pi / 2.0, 0.0, 0.0),
    )
    obj = bpy.context.object
    obj.name = name
    obj.data.materials.append(material)
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
    return obj


def add_arrow(name, direction, center_x, center_y, center_z, length, height, depth, material):
    half_height = height / 2.0
    half_length = length / 2.0
    neck = half_length * 0.18
    tail = half_length * 0.82
    points = [
        (-tail, -half_height * 0.48),
        (neck, -half_height * 0.48),
        (neck, -half_height),
        (half_length, 0.0),
        (neck, half_height),
        (neck, half_height * 0.48),
        (-tail, half_height * 0.48),
    ]
    if direction < 0:
        points = [(-x, z) for x, z in points]

    count = len(points)
    front_y = center_y - depth / 2.0
    back_y = center_y + depth / 2.0
    vertices = [(center_x + x, front_y, center_z + z) for x, z in points]
    vertices.extend((center_x + x, back_y, center_z + z) for x, z in points)
    faces = [tuple(range(count)), tuple(reversed(range(count, count * 2)))]
    for index in range(count):
        next_index = (index + 1) % count
        faces.append((index, count + index, count + next_index, next_index))

    mesh = bpy.data.meshes.new(name + " mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.materials.append(material)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    return obj


def add_motion_ray(name, angle, radius, y, material):
    x = radius * math.cos(angle)
    z = radius * math.sin(angle)
    rotation_y = -angle
    return add_rounded_box(
        name,
        (0.16, 0.045, 0.055),
        (x, y, z),
        material,
        bevel_width=0.022,
        bevel_segments=2,
        rotation=(0.0, rotation_y, 0.0),
    )


def add_area_light(name, location, energy, size, collection):
    light_data = bpy.data.lights.new(name, "AREA")
    light_data.energy = energy
    light_data.shape = "DISK"
    light_data.size = size
    light = bpy.data.objects.new(name, light_data)
    collection.objects.link(light)
    light.location = location
    return light


def setup_scene(wall, asset_collection, studio_collection):
    scene = bpy.context.scene
    camera_data = bpy.data.cameras.new("Wall 1 presentation camera")
    camera = bpy.data.objects.new("Wall 1 presentation camera", camera_data)
    studio_collection.objects.link(camera)
    camera.location = (2.8, -9.0, 1.8)
    camera.rotation_euler = (Vector((0.0, 0.0, 0.0)) - camera.location).to_track_quat("-Z", "Y").to_euler()
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = 6.4
    scene.camera = camera

    add_area_light("Soft key", (-3.8, -5.0, 5.0), 850.0, 4.5, studio_collection)
    add_area_light("Cyan fill", (4.5, -3.0, 2.8), 500.0, 3.5, studio_collection)

    world = bpy.data.worlds.new("Soft rose studio") if not bpy.data.worlds else bpy.data.worlds[0]
    scene.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.42, 0.23, 0.36, 1.0)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.85

    available_engines = {item.identifier for item in scene.render.bl_rna.properties["engine"].enum_items}
    scene.render.engine = "BLENDER_EEVEE_NEXT" if "BLENDER_EEVEE_NEXT" in available_engines else "BLENDER_EEVEE"
    scene.render.resolution_x = 1200
    scene.render.resolution_y = 950
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGB"
    scene.render.film_transparent = False
    scene.render.filepath = str(PREVIEW_PATH)
    scene.view_settings.view_transform = "AgX"
    scene.view_settings.look = "AgX - Medium High Contrast"

    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type == "VIEW_3D":
                space = area.spaces.active
                space.region_3d.view_perspective = "ORTHO"
                space.region_3d.view_location = Vector((0.0, 0.0, 0.0))
                space.region_3d.view_distance = 6.3
                space.region_3d.view_rotation = camera.rotation_euler.to_quaternion()
                space.shading.type = "MATERIAL"
                if hasattr(space.overlay, "show_floor"):
                    space.overlay.show_floor = False
                if hasattr(space.overlay, "show_extras"):
                    space.overlay.show_extras = False

    move_to_collection(wall, asset_collection)
    bpy.ops.object.select_all(action="DESELECT")
    wall.select_set(True)
    bpy.context.view_layer.objects.active = wall


def build_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for material in list(bpy.data.materials):
        if material.users == 0:
            bpy.data.materials.remove(material)

    scene = bpy.context.scene
    asset_collection = bpy.data.collections.new("Wall 1 | Bounce Surface")
    studio_collection = bpy.data.collections.new("Studio | render only")
    scene.collection.children.link(asset_collection)
    scene.collection.children.link(studio_collection)

    plum = make_material("Deep Plum", (0.045, 0.022, 0.075), roughness=0.58)
    berry = make_material("Berry Frame", (0.58, 0.055, 0.31), roughness=0.36)
    cyan = make_material("Bounce Cyan", (0.015, 0.58, 0.68), roughness=0.3)
    white = make_material("Soft White", (0.91, 0.89, 0.94), roughness=0.28)
    dark_plum = make_material("Impact Center Plum", (0.12, 0.035, 0.16), roughness=0.35)
    pale_cyan = make_material("Impact Core", (0.36, 0.86, 0.91), roughness=0.24)

    parts = []
    parts.append(add_rounded_box("Wall body", (3.6, 0.44, 2.3), (0.0, 0.0, 0.0), plum, 0.13, 4))
    parts.append(add_rounded_box("Berry inset frame", (3.39, 0.12, 2.08), (0.0, -0.245, 0.0), berry, 0.055, 3))
    parts.append(add_rounded_box("Elastic cyan face", (3.17, 0.13, 1.86), (0.0, -0.34, 0.0), cyan, 0.10, 4))

    # A raised circular impact pad with two bright rings reads as a springy contact point.
    parts.append(add_front_cylinder("Impact pad", 0.59, 0.12, (0.0, -0.445, 0.0), dark_plum, vertices=48, bevel_width=0.018))
    parts.append(add_front_torus("Outer impact ring", 0.53, 0.075, (0.0, -0.52, 0.0), white, major_segments=40, minor_segments=8))
    parts.append(add_front_torus("Inner elastic ring", 0.30, 0.042, (0.0, -0.55, 0.0), berry, major_segments=32, minor_segments=8))
    parts.append(add_front_cylinder("Impact core", 0.17, 0.07, (0.0, -0.575, 0.0), pale_cyan, vertices=40, bevel_width=0.012))

    # Outward arrows and radial ticks make the rebound direction legible at a glance.
    for direction in (-1, 1):
        sign = "Left" if direction < 0 else "Right"
        parts.append(add_arrow(sign + " rebound arrow shadow", direction, direction * 1.08, -0.445, 0.0, 0.65, 0.38, 0.065, dark_plum))
        parts.append(add_arrow(sign + " rebound arrow", direction, direction * 1.08, -0.49, 0.0, 0.53, 0.27, 0.045, white))

    for index, angle in enumerate((math.pi / 4.0, 3.0 * math.pi / 4.0, 5.0 * math.pi / 4.0, 7.0 * math.pi / 4.0)):
        parts.append(add_motion_ray("Impact ray %02d" % (index + 1), angle, 0.83, -0.425, white))

    prepare_active(parts[0])
    for part in parts[1:]:
        part.select_set(True)
    bpy.ops.object.join()
    wall = bpy.context.object
    wall.name = "Wall_1_BounceSurface"
    wall.data.name = "Wall_1 low-poly rebound wall"
    wall.data.use_fake_user = False

    setup_scene(wall, asset_collection, studio_collection)

    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_PATH))
    bpy.ops.render.render(write_still=True)

    prepare_active(wall)
    bpy.ops.export_scene.fbx(
        filepath=str(FBX_PATH),
        use_selection=True,
        object_types={"MESH"},
        apply_scale_options="FBX_SCALE_ALL",
        axis_forward="-Z",
        axis_up="Y",
        use_mesh_modifiers=True,
        add_leaf_bones=False,
        bake_anim=False,
    )


if __name__ == "__main__":
    build_scene()
