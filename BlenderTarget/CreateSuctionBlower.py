import math
from pathlib import Path

import bpy
from mathutils import Vector


OUTPUT_DIR = Path(__file__).resolve().parent
BLEND_PATH = OUTPUT_DIR / "SuctionBlowerPair.blend"
PREVIEW_PATH = OUTPUT_DIR / "SuctionBlowerPair_preview.png"
MODEL_DIR = OUTPUT_DIR.parent / "Assets" / "Project" / "Resoruce_game" / "Model"
ENTRY_FBX = MODEL_DIR / "Portal_Entry_Visual.fbx"
EXIT_FBX = MODEL_DIR / "Portal_Exit_Visual.fbx"
PAIR_FBX = MODEL_DIR / "PortalPair_Visual.fbx"


def make_material(name, color, roughness=0.4, metallic=0.0, emission=0.0):
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


def move_to_collection(obj, collection):
    for current in list(obj.users_collection):
        current.objects.unlink(obj)
    collection.objects.link(obj)


def prepare_active(obj):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def add_horn_shell(name, outer, inner, frame, lip, segments=28):
    # Cross-section profile: broad mouth at the front, narrowing to a short speaker-like throat.
    # Each pair is (depth along Y, radius in the XZ plane, material band).
    profile = [
        (-0.56, 0.86, "outer"),
        (-0.51, 0.83, "outer"),
        (-0.33, 0.74, "outer"),
        (-0.10, 0.58, "outer"),
        (0.16, 0.39, "outer"),
        (0.40, 0.28, "outer"),
        (0.83, 0.28, "frame"),
        (0.87, 0.23, "lip"),
        (0.87, 0.18, "inner"),
        (0.55, 0.18, "inner"),
        (0.37, 0.23, "inner"),
        (0.15, 0.38, "inner"),
        (-0.10, 0.56, "inner"),
        (-0.34, 0.71, "inner"),
        (-0.54, 0.77, "inner"),
        (-0.56, 0.77, "lip"),
    ]
    materials = {"outer": outer, "inner": inner, "frame": frame, "lip": lip}
    vertices = []
    faces = []
    material_ids = []
    for depth, radius, _band in profile:
        for index in range(segments):
            angle = index * math.tau / segments
            vertices.append((radius * math.cos(angle), depth, radius * math.sin(angle)))

    for profile_index in range(len(profile)):
        next_profile = (profile_index + 1) % len(profile)
        for index in range(segments):
            next_index = (index + 1) % segments
            faces.append((
                profile_index * segments + index,
                next_profile * segments + index,
                next_profile * segments + next_index,
                profile_index * segments + next_index,
            ))
            material_ids.append(profile[profile_index][2])

    mesh = bpy.data.meshes.new(name + " mesh")
    mesh.from_pydata(vertices, [], faces)
    for band in ("outer", "inner", "frame", "lip"):
        mesh.materials.append(materials[band])
    band_ids = {band: index for index, band in enumerate(("outer", "inner", "frame", "lip"))}
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    for polygon, band in zip(mesh.polygons, material_ids):
        polygon.material_index = band_ids[band]
        polygon.use_smooth = True
    return obj


def cone_inner_depth(radius):
    # Approximate the inside wall between throat radius .18 at Y=.55 and mouth radius .77 at Y=-.54.
    return 0.55 - (radius - 0.18) * (1.09 / 0.59)


def add_flow_arrow(name, angle, center_radius, length, width, direction, material):
    radial = Vector((math.cos(angle), math.sin(angle)))
    tangent = Vector((-math.sin(angle), math.cos(angle)))
    # The arrow is modeled flat on the inside funnel surface; direction is toward or away from the throat.
    sign = -1.0 if direction == "in" else 1.0
    outline = [
        (-length * 0.50, -width * 0.20),
        (length * 0.06, -width * 0.20),
        (length * 0.06, -width * 0.48),
        (length * 0.50, 0.0),
        (length * 0.06, width * 0.48),
        (length * 0.06, width * 0.20),
        (-length * 0.50, width * 0.20),
    ]
    vertices = []
    for along, across in outline:
        radius = center_radius + sign * along
        point = radial * radius + tangent * across
        depth = cone_inner_depth(max(0.2, min(radius, 0.76))) - 0.025
        vertices.append((point.x, depth, point.y))
    mesh = bpy.data.meshes.new(name + " mesh")
    mesh.from_pydata(vertices, [], [tuple(range(len(vertices)))])
    mesh.materials.append(material)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    return obj


def add_front_cylinder(name, radius, depth, y, material, vertices=24):
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=vertices,
        radius=radius,
        depth=depth,
        location=(0.0, y, 0.0),
        rotation=(math.pi / 2.0, 0.0, 0.0),
    )
    obj = bpy.context.object
    obj.name = name
    obj.data.materials.append(material)
    return obj


def add_front_torus(name, radius, tube, y, material, major_segments=28, minor_segments=5):
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


def add_front_dot(name, x, z, y, radius, material, vertices=12):
    obj = add_front_cylinder(name, radius, 0.035, y, material, vertices=vertices)
    obj.location.x = x
    obj.location.z = z
    return obj


def add_pair_arrow(name, material, collection):
    points = [(-0.19, -0.065), (0.015, -0.065), (0.015, -0.14), (0.23, 0.0), (0.015, 0.14), (0.015, 0.065), (-0.19, 0.065)]
    depth = -0.32
    thickness = 0.055
    count = len(points)
    vertices = [(x, depth - thickness / 2.0, z) for x, z in points]
    vertices.extend((x, depth + thickness / 2.0, z) for x, z in points)
    faces = [tuple(range(count)), tuple(reversed(range(count, count * 2)))]
    for index in range(count):
        nxt = (index + 1) % count
        faces.append((index, count + index, count + nxt, nxt))
    mesh = bpy.data.meshes.new(name + " mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.materials.append(material)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    collection.objects.link(obj)
    return obj


def add_text(name, body, location, size, material, collection):
    data = bpy.data.curves.new(name, type="FONT")
    data.body = body
    data.align_x = "CENTER"
    data.align_y = "CENTER"
    data.size = size
    data.extrude = 0.002
    data.bevel_depth = 0.001
    obj = bpy.data.objects.new(name, data)
    collection.objects.link(obj)
    obj.rotation_euler[0] = math.pi / 2.0
    obj.location = location
    data.materials.append(material)
    return obj


def build_horn(name, x_position, outer, inner, throat_material, lip_material, flow_material, flow_direction, collection):
    parts = [add_horn_shell(name + " | flared speaker horn", outer, inner, plum, lip_material)]
    parts.append(add_front_torus(name + " | single mouth lip", 0.815, 0.052, -0.545, lip_material))
    parts.append(add_front_torus(name + " | throat collar", 0.245, 0.035, 0.835, plum, major_segments=24, minor_segments=4))
    parts.append(add_front_cylinder(name + " | dark throat", 0.177, 0.035, 0.825, throat_material, vertices=24))

    # A few straight arrows on the cone make the one-way suction/blow action unmistakable.
    if flow_direction == "in":
        arrow_radius, arrow_length = 0.61, 0.24
    else:
        arrow_radius, arrow_length = 0.43, 0.28
    for index, angle in enumerate((0.0, math.pi / 2.0, math.pi, 3.0 * math.pi / 2.0)):
        parts.append(add_flow_arrow(
            name + " | flow arrow %d" % (index + 1),
            angle,
            arrow_radius,
            arrow_length,
            0.16,
            flow_direction,
            flow_material,
        ))

    # A tiny glowing bead at the blower throat suggests the bullet being expelled, not a vortex.
    if flow_direction == "out":
        parts.append(add_front_dot(name + " | outgoing bead", 0.0, 0.0, 0.785, 0.075, flow_material, vertices=12))

    prepare_active(parts[0])
    for part in parts[1:]:
        part.select_set(True)
    bpy.ops.object.join()
    horn = bpy.context.object
    horn.name = name
    horn.data.name = name + " | single low-poly mesh"
    horn.location = (x_position, 0.0, 0.0)
    move_to_collection(horn, collection)
    return horn


def add_area_light(name, location, energy, size, collection):
    data = bpy.data.lights.new(name, "AREA")
    data.energy = energy
    data.shape = "DISK"
    data.size = size
    obj = bpy.data.objects.new(name, data)
    collection.objects.link(obj)
    obj.location = location
    return obj


def setup_scene(inlet, outlet, presentation, studio, ivory, label_material):
    scene = bpy.context.scene
    for index, x in enumerate((-0.66, -0.39, 0.39, 0.66)):
        dot = add_front_cylinder("Transfer cue | dot %d" % (index + 1), 0.055, 0.04, -0.31, ivory, vertices=12)
        dot.location.x = x
        move_to_collection(dot, presentation)
    add_pair_arrow("Transfer cue | inlet to outlet", ivory, presentation)
    add_text("Label | suction", "A  HUT VAO", (-1.72, -0.34, -1.16), 0.18, label_material, presentation)
    add_text("Label | blower", "B  THOI RA", (1.72, -0.34, -1.16), 0.18, label_material, presentation)

    camera_data = bpy.data.cameras.new("Suction blower presentation camera")
    camera = bpy.data.objects.new("Suction blower presentation camera", camera_data)
    studio.objects.link(camera)
    camera.location = (0.0, -9.2, 5.4)
    camera.rotation_euler = (Vector((0.0, 0.0, -0.05)) - camera.location).to_track_quat("-Z", "Y").to_euler()
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = 6.6
    scene.camera = camera

    add_area_light("Soft key", (-3.0, -5.0, 5.2), 800.0, 4.0, studio)
    add_area_light("Cool fill", (4.0, -3.0, 1.6), 420.0, 3.2, studio)
    add_area_light("Pink rim", (0.0, 2.0, 4.0), 480.0, 3.0, studio)

    world = bpy.data.worlds.new("Cotton candy studio") if not bpy.data.worlds else bpy.data.worlds[0]
    scene.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.62, 0.36, 0.54, 1.0)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.82

    available_engines = {item.identifier for item in scene.render.bl_rna.properties["engine"].enum_items}
    scene.render.engine = "BLENDER_EEVEE_NEXT" if "BLENDER_EEVEE_NEXT" in available_engines else "BLENDER_EEVEE"
    scene.render.resolution_x = 1200
    scene.render.resolution_y = 820
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
                space.region_3d.view_location = Vector((0.0, 0.0, -0.04))
                space.region_3d.view_distance = 7.5
                space.region_3d.view_rotation = camera.rotation_euler.to_quaternion()
                space.shading.type = "MATERIAL"
                if hasattr(space.overlay, "show_floor"):
                    space.overlay.show_floor = False
                if hasattr(space.overlay, "show_extras"):
                    space.overlay.show_extras = False

    inlet.select_set(False)
    outlet.select_set(True)
    bpy.context.view_layer.objects.active = outlet


def export_fbx(path, objects, reset_locations=False):
    old_locations = {obj.name: obj.location.copy() for obj in objects}
    if reset_locations:
        for obj in objects:
            obj.location = (0.0, 0.0, 0.0)
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.export_scene.fbx(
        filepath=str(path),
        use_selection=True,
        object_types={"MESH"},
        apply_scale_options="FBX_SCALE_ALL",
        axis_forward="-Z",
        axis_up="Y",
        use_mesh_modifiers=True,
        add_leaf_bones=False,
        bake_anim=False,
    )
    for obj in objects:
        obj.location = old_locations[obj.name]


def build_scene():
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for material in list(bpy.data.materials):
        if material.users == 0:
            bpy.data.materials.remove(material)

    scene = bpy.context.scene
    asset_collection = bpy.data.collections.new("HORNS | editable one-way endpoints")
    presentation = bpy.data.collections.new("PRESENTATION ONLY | arrows and labels")
    studio = bpy.data.collections.new("STUDIO | camera and lights")
    scene.collection.children.link(asset_collection)
    scene.collection.children.link(presentation)
    scene.collection.children.link(studio)

    global plum
    plum = make_material("Deep plum | speaker frame", (0.105, 0.045, 0.17), roughness=0.42)
    ivory = make_material("Vanilla | directional arrows", (1.0, 0.91, 0.74), roughness=0.27, emission=0.1)
    label_material = make_material("Plum | labels", (0.31, 0.13, 0.39), roughness=0.5)
    dark_throat = make_material("Dark throat | no swirl", (0.045, 0.025, 0.085), roughness=0.26)
    suction_outer = make_material("Suction horn | strawberry shell", (0.94, 0.16, 0.41), roughness=0.32, metallic=0.08)
    suction_inner = make_material("Suction horn | shaded rose inside", (0.47, 0.045, 0.20), roughness=0.38)
    suction_flow = make_material("Suction horn | cream in-arrows", (1.0, 0.72, 0.76), roughness=0.24, emission=0.12)
    blower_outer = make_material("Blower horn | candy cyan shell", (0.055, 0.70, 0.78), roughness=0.3, metallic=0.08)
    blower_inner = make_material("Blower horn | shaded teal inside", (0.02, 0.34, 0.45), roughness=0.36)
    blower_flow = make_material("Blower horn | cream out-arrows", (0.71, 1.0, 0.91), roughness=0.23, emission=0.14)

    inlet = build_horn(
        "Portal_Entry_Visual",
        -1.72,
        suction_outer,
        suction_inner,
        dark_throat,
        ivory,
        suction_flow,
        "in",
        asset_collection,
    )
    outlet = build_horn(
        "Portal_Exit_Visual",
        1.72,
        blower_outer,
        blower_inner,
        dark_throat,
        ivory,
        blower_flow,
        "out",
        asset_collection,
    )
    setup_scene(inlet, outlet, presentation, studio, ivory, label_material)

    MODEL_DIR.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_PATH))
    bpy.ops.render.render(write_still=True)
    export_fbx(ENTRY_FBX, [inlet], reset_locations=True)
    export_fbx(EXIT_FBX, [outlet], reset_locations=True)
    export_fbx(PAIR_FBX, [inlet, outlet], reset_locations=False)

    for obj in (inlet, outlet):
        triangles = sum(max(0, len(poly.vertices) - 2) for poly in obj.data.polygons)
        print("HORN_MESH_STATS", obj.name, "verts=", len(obj.data.vertices), "faces=", len(obj.data.polygons), "tris=", triangles)


if __name__ == "__main__":
    build_scene()
