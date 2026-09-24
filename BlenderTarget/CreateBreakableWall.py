import math
from pathlib import Path

import bpy
from mathutils import Vector


OUTPUT_DIR = Path(__file__).resolve().parent
BLEND_PATH = OUTPUT_DIR / "Wall_And_DestroyTarget.blend"
PREVIEW_PATH = OUTPUT_DIR / "Wall_And_DestroyTarget_preview.png"


def make_material(name, color, roughness=0.42, metallic=0.0, emission=0.0):
    material = bpy.data.materials.new(name)
    material.diffuse_color = (*color, 1.0)
    material.use_nodes = True
    shader = material.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = (*color, 1.0)
    shader.inputs["Roughness"].default_value = roughness
    shader.inputs["Metallic"].default_value = metallic
    if emission:
        emission_color = shader.inputs.get("Emission Color") or shader.inputs.get("Emission")
        if emission_color:
            emission_color.default_value = (*color, 1.0)
        strength = shader.inputs.get("Emission Strength")
        if strength:
            strength.default_value = emission
    return material


def prepare_active(obj):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def move_to_collection(obj, collection):
    for current in list(obj.users_collection):
        current.objects.unlink(obj)
    collection.objects.link(obj)


def apply_modifier(obj, modifier):
    prepare_active(obj)
    bpy.ops.object.modifier_apply(modifier=modifier.name)


def add_rounded_box(name, dimensions, location, material, bevel_width, bevel_segments=2, rotation=(0.0, 0.0, 0.0)):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = dimensions
    prepare_active(obj)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(material)
    bevel = obj.modifiers.new("Soft molded corners", "BEVEL")
    bevel.width = bevel_width
    bevel.segments = bevel_segments
    bevel.limit_method = "ANGLE"
    apply_modifier(obj, bevel)
    for polygon in obj.data.polygons:
        polygon.use_smooth = len(polygon.vertices) == 4
    return obj


def add_front_cylinder(name, radius, depth, y, material, vertices=32, bevel=0.0):
    chamfer = min(bevel, depth * 0.42, radius * 0.22) if bevel > 0.0 else 0.0
    if chamfer:
        profiles = [
            (-depth / 2.0, radius - chamfer),
            (-depth / 2.0 + chamfer, radius),
            (depth / 2.0 - chamfer, radius),
            (depth / 2.0, radius - chamfer),
        ]
    else:
        profiles = [(-depth / 2.0, radius), (depth / 2.0, radius)]
    vertices_data = []
    faces = []
    for local_y, profile_radius in profiles:
        for index in range(vertices):
            angle = index * math.tau / vertices
            vertices_data.append((profile_radius * math.cos(angle), y + local_y, profile_radius * math.sin(angle)))
    for profile_index in range(len(profiles) - 1):
        current = profile_index * vertices
        following = (profile_index + 1) * vertices
        for index in range(vertices):
            nxt = (index + 1) % vertices
            faces.append((current + index, following + index, following + nxt, current + nxt))
    faces.append(tuple(range(vertices)))
    back_start = (len(profiles) - 1) * vertices
    faces.append(tuple(reversed(range(back_start, back_start + vertices))))
    mesh = bpy.data.meshes.new(name + " mesh")
    mesh.from_pydata(vertices_data, [], faces)
    mesh.materials.append(material)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    for polygon in obj.data.polygons:
        polygon.use_smooth = len(polygon.vertices) == 4
    return obj


def add_front_torus(name, radius, tube, y, material, major_segments=28, minor_segments=6):
    bpy.ops.mesh.primitive_torus_add(
        major_segments=major_segments,
        minor_segments=minor_segments,
        major_radius=radius,
        minor_radius=tube,
        location=(0.0, y, 0.0),
        rotation=(math.pi / 2.0, 0.0, 0.0),
    )
    obj = bpy.context.object
    obj.name = name
    obj.data.materials.append(material)
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
    return obj


def add_front_arrow(name, angle, center_radius, length, width, material, depth=-0.055):
    # A short, flat arrow points radially inward to show the exact impact point.
    direction = -1.0
    radial = Vector((math.cos(angle), math.sin(angle)))
    tangent = Vector((-math.sin(angle), math.cos(angle)))
    outline = [
        (-length * 0.50, -width * 0.20),
        (length * 0.06, -width * 0.20),
        (length * 0.06, -width * 0.50),
        (length * 0.50, 0.0),
        (length * 0.06, width * 0.50),
        (length * 0.06, width * 0.20),
        (-length * 0.50, width * 0.20),
    ]
    points = []
    for along, across in outline:
        radius = center_radius + direction * along
        point = radial * radius + tangent * across
        points.append((point.x, point.y))
    thickness = 0.045
    vertices = [(x, depth - thickness / 2.0, z) for x, z in points]
    vertices.extend((x, depth + thickness / 2.0, z) for x, z in points)
    count = len(points)
    faces = [tuple(range(count)), tuple(reversed(range(count, count * 2)))]
    for index in range(count):
        nxt = (index + 1) % count
        faces.append((index, count + index, count + nxt, nxt))
    mesh = bpy.data.meshes.new(name + " mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.materials.append(material)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    return obj


def add_area_light(name, location, energy, size, collection):
    light_data = bpy.data.lights.new(name, "AREA")
    light_data.energy = energy
    light_data.shape = "DISK"
    light_data.size = size
    light = bpy.data.objects.new(name, light_data)
    collection.objects.link(light)
    light.location = location
    return light


def make_wall_body(collection, plum, red, light_red, gold, ivory):
    parts = []
    parts.append(add_rounded_box("Wall body | deep plum shell", (3.62, 0.48, 2.52), (0.0, 0.0, 0.0), plum, 0.17, 3))
    parts.append(add_rounded_box("Wall face | red armor plate", (3.43, 0.16, 2.33), (0.0, -0.275, 0.0), red, 0.135, 3))
    parts.append(add_rounded_box("Wall inset | bright red panel", (3.24, 0.07, 2.14), (0.0, -0.39, 0.0), light_red, 0.10, 2))

    # Short diagonal hazard bars on both sides make this read as a warning barricade.
    for side in (-1, 1):
        for index, z in enumerate((-0.52, 0.0, 0.52)):
            stripe_material = gold if index % 2 == 0 else plum
            parts.append(add_rounded_box(
                "Warning stripe | %s %d" % ("L" if side < 0 else "R", index + 1),
                (0.16, 0.045, 0.40),
                (side * 1.30, -0.455, z),
                stripe_material,
                0.035,
                2,
                rotation=(0.0, 0.42, 0.0),
            ))

    # Gold corner rails and tiny fasteners signal a reinforced, blocking barrier.
    for side in (-1, 1):
        parts.append(add_rounded_box(
            "Wall frame | side rail %s" % ("L" if side < 0 else "R"),
            (0.105, 0.055, 1.88),
            (side * 1.57, -0.445, 0.0),
            gold,
            0.04,
            2,
        ))
    for side_x in (-1.47, 1.47):
        for side_z in (-0.91, 0.91):
            bolt = add_front_cylinder("Wall frame | corner bolt", 0.052, 0.045, -0.46, ivory, vertices=12, bevel=0.012)
            bolt.location.x = side_x
            bolt.location.z = side_z
            parts.append(bolt)

    # Amber warning beacons sit on the upper corners of the barrier.
    for side in (-1, 1):
        beacon = add_front_cylinder("Warning beacon | amber lens", 0.095, 0.075, -0.47, gold, vertices=16, bevel=0.018)
        beacon.location.x = side * 1.38
        beacon.location.z = 1.08
        parts.append(beacon)

    prepare_active(parts[0])
    for part in parts[1:]:
        part.select_set(True)
    bpy.ops.object.join()
    body = bpy.context.object
    body.name = "Wall_Blocker"
    body.data.name = "Wall_Blocker | low-poly mesh"
    move_to_collection(body, collection)
    return body


def make_weak_point(collection, plum, dark_red, gold, ivory, cyan):
    parts = []
    parts.append(add_front_cylinder("Weak point | armored backing", 0.59, 0.13, -0.455, plum, vertices=32, bevel=0.025))
    parts.append(add_front_torus("Weak point | gold outer ring", 0.49, 0.065, -0.555, gold, major_segments=28, minor_segments=6))
    parts.append(add_front_cylinder("Weak point | ivory target plate", 0.375, 0.075, -0.55, ivory, vertices=28, bevel=0.018))
    parts.append(add_front_torus("Weak point | red inner ring", 0.275, 0.045, -0.615, dark_red, major_segments=24, minor_segments=5))
    parts.append(add_front_cylinder("Weak point | cyan shot core", 0.18, 0.085, -0.615, cyan, vertices=24, bevel=0.025))
    parts.append(add_front_cylinder("Weak point | tiny bullseye", 0.067, 0.035, -0.675, ivory, vertices=16, bevel=0.012))

    for index, angle in enumerate((0.0, math.pi / 2.0, math.pi, 3.0 * math.pi / 2.0)):
        parts.append(add_front_arrow(
            "Weak point | inward marker %d" % (index + 1),
            angle,
            0.72,
            0.24,
            0.15,
            ivory,
            depth=-0.48,
        ))

    prepare_active(parts[0])
    for part in parts[1:]:
        part.select_set(True)
    bpy.ops.object.join()
    weak_point = bpy.context.object
    weak_point.name = "Destroy_Target"
    weak_point.data.name = "Destroy_Target | separate target mesh"
    move_to_collection(weak_point, collection)

    # Put the object origin at the visible impact center for easy collider placement in Unity.
    bpy.context.scene.cursor.location = (0.0, -0.56, 0.0)
    prepare_active(weak_point)
    bpy.ops.object.origin_set(type="ORIGIN_CURSOR")
    # Float the target clearly in front of the wall; it is not part of the wall mesh.
    weak_point.location.y -= 0.30
    return weak_point


def setup_scene(body, weak_point, asset_collection, studio_collection):
    scene = bpy.context.scene
    camera_data = bpy.data.cameras.new("Warning barrier presentation camera")
    camera = bpy.data.objects.new("Warning barrier presentation camera", camera_data)
    studio_collection.objects.link(camera)
    camera.location = (2.6, -9.0, 2.3)
    camera.rotation_euler = (Vector((0.0, 0.0, 0.0)) - camera.location).to_track_quat("-Z", "Y").to_euler()
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = 5.9
    scene.camera = camera

    add_area_light("Soft key", (-3.8, -5.0, 5.0), 850.0, 4.5, studio_collection)
    add_area_light("Warm fill", (4.5, -3.0, 2.8), 460.0, 3.4, studio_collection)
    add_area_light("Rim light", (0.0, 2.0, 3.5), 360.0, 3.0, studio_collection)

    world = bpy.data.worlds.new("Soft rose studio") if not bpy.data.worlds else bpy.data.worlds[0]
    scene.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.45, 0.22, 0.34, 1.0)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.82

    available_engines = {item.identifier for item in scene.render.bl_rna.properties["engine"].enum_items}
    scene.render.engine = "BLENDER_EEVEE_NEXT" if "BLENDER_EEVEE_NEXT" in available_engines else "BLENDER_EEVEE"
    scene.render.resolution_x = 1200
    scene.render.resolution_y = 900
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
                space.region_3d.view_distance = 6.8
                space.region_3d.view_rotation = camera.rotation_euler.to_quaternion()
                space.shading.type = "MATERIAL"
                if hasattr(space.overlay, "show_floor"):
                    space.overlay.show_floor = False
                if hasattr(space.overlay, "show_extras"):
                    space.overlay.show_extras = False

    prepare_active(weak_point)
    body.select_set(True)
    bpy.context.view_layer.objects.active = weak_point


def build_scene():
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for material in list(bpy.data.materials):
        if material.users == 0:
            bpy.data.materials.remove(material)

    scene = bpy.context.scene
    asset_collection = bpy.data.collections.new("GAMEPLAY OBJECTS | Wall + separate Destroy Target")
    studio_collection = bpy.data.collections.new("STUDIO | camera and lights")
    scene.collection.children.link(asset_collection)
    scene.collection.children.link(studio_collection)

    plum = make_material("Deep plum | reinforced edge", (0.105, 0.035, 0.12), roughness=0.48)
    red = make_material("Barrier red | main armor", (0.68, 0.025, 0.11), roughness=0.34, metallic=0.06)
    light_red = make_material("Barrier red | bright inset", (0.9, 0.075, 0.16), roughness=0.35)
    dark_red = make_material("Dark cherry | target ring", (0.38, 0.012, 0.06), roughness=0.3)
    gold = make_material("Warm gold | warning trim", (1.0, 0.48, 0.08), roughness=0.28, metallic=0.12)
    ivory = make_material("Soft ivory | target", (1.0, 0.89, 0.68), roughness=0.26)
    cyan = make_material("Bright cyan | shoot core", (0.04, 0.83, 0.91), roughness=0.24, metallic=0.08, emission=0.12)

    body = make_wall_body(asset_collection, plum, red, light_red, gold, ivory)
    weak_point = make_weak_point(asset_collection, plum, dark_red, gold, ivory, cyan)
    setup_scene(body, weak_point, asset_collection, studio_collection)

    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_PATH))
    bpy.ops.render.render(write_still=True)

    for obj in (body, weak_point):
        triangles = sum(max(0, len(poly.vertices) - 2) for poly in obj.data.polygons)
        print("WALL_MESH_STATS", obj.name, "verts=", len(obj.data.vertices), "faces=", len(obj.data.polygons), "tris=", triangles, "location=", tuple(round(v, 3) for v in obj.location))


if __name__ == "__main__":
    build_scene()
