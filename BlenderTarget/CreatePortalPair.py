import math
from pathlib import Path

import bpy
from mathutils import Vector


OUTPUT_DIR = Path(__file__).resolve().parent
BLEND_PATH = OUTPUT_DIR / "PortalPair_Tubes.blend"
PREVIEW_PATH = OUTPUT_DIR / "PortalPair_Tubes_preview.png"
MODEL_DIR = OUTPUT_DIR.parent / "Assets" / "Project" / "Resoruce_game" / "Model"
ENTRY_FBX = MODEL_DIR / "Portal_Entry_Visual.fbx"
EXIT_FBX = MODEL_DIR / "Portal_Exit_Visual.fbx"
PAIR_FBX = MODEL_DIR / "PortalPair_Visual.fbx"


def make_material(name, color, roughness=0.38, metallic=0.0, emission=0.0):
    material = bpy.data.materials.new(name)
    material.diffuse_color = (*color, 1.0)
    material.use_nodes = True
    shader = material.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = (*color, 1.0)
    shader.inputs["Roughness"].default_value = roughness
    shader.inputs["Metallic"].default_value = metallic
    if emission > 0.0:
        emission_input = shader.inputs.get("Emission Color") or shader.inputs.get("Emission")
        if emission_input:
            emission_input.default_value = (*color, 1.0)
        strength_input = shader.inputs.get("Emission Strength")
        if strength_input:
            strength_input.default_value = emission
    return material


def move_to_collection(obj, collection):
    for current in list(obj.users_collection):
        current.objects.unlink(obj)
    collection.objects.link(obj)


def prepare_active(obj):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def apply_modifier(obj, modifier):
    prepare_active(obj)
    bpy.ops.object.modifier_apply(modifier=modifier.name)


def add_front_cylinder(name, radius, depth, y, material, vertices=24, bevel=0.0):
    chamfer = min(bevel, depth * 0.42, radius * 0.22) if bevel > 0.0 else 0.0
    profiles = [
        (-depth / 2.0, radius - chamfer),
        (-depth / 2.0 + chamfer, radius),
        (depth / 2.0 - chamfer, radius),
        (depth / 2.0, radius - chamfer),
    ]
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


def add_front_torus(name, radius, tube, y, material, major_segments=24, minor_segments=5):
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


def add_short_nozzle(name, front_y, back_y, outer_front, outer_back, inner_front, inner_back, outer_material, inner_material, segments=20):
    # A short tapered hollow barrel sits behind the glowing portal face.
    profiles = [
        (front_y, outer_front),
        (back_y, outer_back),
        (back_y, inner_back),
        (front_y, inner_front),
    ]
    vertices = []
    faces = []
    for local_y, radius in profiles:
        for index in range(segments):
            angle = index * math.tau / segments
            vertices.append((radius * math.cos(angle), local_y, radius * math.sin(angle)))
    for index in range(segments):
        nxt = (index + 1) % segments
        faces.append((index, segments + index, segments + nxt, nxt))
        inner_back = 2 * segments
        inner_front = 3 * segments
        faces.append((inner_back + index, inner_front + index, inner_front + nxt, inner_back + nxt))

    mesh = bpy.data.meshes.new(name + " mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.materials.append(outer_material)
    mesh.materials.append(inner_material)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    for index, polygon in enumerate(obj.data.polygons):
        polygon.material_index = index % 2
        polygon.use_smooth = True
    return obj


def add_front_dot(name, x, z, y, radius, material, vertices=12):
    obj = add_front_cylinder(name, radius, 0.035, y, material, vertices=vertices, bevel=0.009)
    obj.location.x = x
    obj.location.z = z
    return obj


def add_spiral(name, material):
    steps = 28
    turns = 1.7
    max_radius = 0.365
    half_width = 0.035
    vertices = []
    faces = []
    for index in range(steps + 1):
        fraction = index / steps
        angle = fraction * turns * math.tau
        radius = 0.035 + fraction * max_radius
        x = radius * math.cos(angle)
        z = radius * math.sin(angle)
        tangent_x = -math.sin(angle)
        tangent_z = math.cos(angle)
        # Offset perpendicular to the spiral tangent, keeping the ribbon on the portal face.
        vertices.append((x - tangent_z * half_width, -0.207, z + tangent_x * half_width))
        vertices.append((x + tangent_z * half_width, -0.207, z - tangent_x * half_width))
        if index > 0:
            base = index * 2
            faces.append((base - 2, base - 1, base + 1, base))
    mesh = bpy.data.meshes.new(name + " mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.materials.append(material)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    return obj


def add_star(name, x, z, y, outer_radius, inner_radius, material):
    vertices = []
    for index in range(8):
        angle = math.pi * 0.5 + index * math.pi / 4.0
        radius = outer_radius if index % 2 == 0 else inner_radius
        vertices.append((x + radius * math.cos(angle), y, z + radius * math.sin(angle)))
    mesh = bpy.data.meshes.new(name + " mesh")
    mesh.from_pydata(vertices, [], [tuple(range(8))])
    mesh.materials.append(material)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    return obj


def add_flow_arrow(name, center_x, y, z, material):
    points = [
        (-0.19, -0.065),
        (0.015, -0.065),
        (0.015, -0.145),
        (0.23, 0.0),
        (0.015, 0.145),
        (0.015, 0.065),
        (-0.19, 0.065),
    ]
    thickness = 0.055
    count = len(points)
    vertices = [(center_x + x, y - thickness / 2, z + dz) for x, dz in points]
    vertices.extend((center_x + x, y + thickness / 2, z + dz) for x, dz in points)
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
    # Text's local front (+Z) faces the camera at -Y after this rotation.
    obj.rotation_euler[0] = math.pi / 2.0
    obj.location = location
    data.materials.append(material)
    return obj


def build_endpoint(name, x_position, primary, accent, collection):
    parts = []
    parts.append(add_short_nozzle(name + " | tapered nozzle body", 0.12, 0.72, 0.69, 0.57, 0.53, 0.42, plum, dark_plum))
    parts.append(add_front_torus(name + " | rear nozzle collar", 0.56, 0.025, 0.68, primary, major_segments=20, minor_segments=4))
    parts.append(add_front_cylinder(name + " | plum backing", 0.89, 0.24, 0.0, plum, vertices=24, bevel=0.055))
    parts.append(add_front_cylinder(name + " | inset backing", 0.77, 0.065, -0.135, dark_plum, vertices=24, bevel=0.02))
    parts.append(add_front_torus(name + " | bright outer halo", 0.79, 0.035, -0.177, cream, major_segments=24, minor_segments=4))
    parts.append(add_front_torus(name + " | portal ring", 0.68, 0.12, -0.205, primary, major_segments=24, minor_segments=6))
    parts.append(add_front_cylinder(name + " | portal aperture", 0.55, 0.065, -0.16, aperture, vertices=24, bevel=0.012))
    parts.append(add_front_torus(name + " | inner light ring", 0.50, 0.028, -0.207, accent, major_segments=24, minor_segments=4))
    parts.append(add_spiral(name + " | vortex ribbon", accent))
    # A soft center bead and two tiny light points give the swirl a friendly, animated feel.
    parts.append(add_front_dot(name + " | swirl core", -0.01, 0.015, -0.238, 0.064, cream, vertices=12))
    parts.append(add_front_dot(name + " | orbit bead 1", 0.31, 0.16, -0.234, 0.032, primary, vertices=10))
    parts.append(add_front_dot(name + " | orbit bead 2", -0.27, -0.21, -0.234, 0.027, cream, vertices=10))
    # Three molded rivets and one little sparkle keep the face decorative without much geometry.
    for index, angle in enumerate((math.pi / 4.0, math.pi, 7.0 * math.pi / 4.0)):
        parts.append(add_front_dot(
            name + " | rivet %d" % (index + 1),
            0.84 * math.cos(angle),
            0.84 * math.sin(angle),
            -0.168,
            0.034,
            accent,
            vertices=10,
        ))
    parts.append(add_star(name + " | sparkle", 0.47, 0.74, -0.19, 0.10, 0.035, cream))

    prepare_active(parts[0])
    for part in parts[1:]:
        part.select_set(True)
    bpy.ops.object.join()
    endpoint = bpy.context.object
    endpoint.name = name
    endpoint.data.name = name + " | low-poly single mesh"
    endpoint.location = (x_position, 0.0, 0.0)
    endpoint.data.use_fake_user = False
    move_to_collection(endpoint, collection)
    return endpoint


def add_area_light(name, location, energy, size, collection):
    data = bpy.data.lights.new(name, "AREA")
    data.energy = energy
    data.shape = "DISK"
    data.size = size
    obj = bpy.data.objects.new(name, data)
    collection.objects.link(obj)
    obj.location = location
    return obj


def setup_studio(entry, exit_obj, presentation_collection, studio_collection):
    scene = bpy.context.scene
    # Dots and a compact arrow explain A -> B in the sample composition only.
    for index, x in enumerate((-0.66, -0.39, 0.39, 0.66)):
        dot = add_front_cylinder("Flow cue | dot %d" % (index + 1), 0.055, 0.04, -0.31, cream, vertices=12, bevel=0.008)
        dot.location.x = x
        move_to_collection(dot, presentation_collection)
    arrow = add_flow_arrow("Flow cue | A to B arrow", 0.0, -0.33, 0.0, cream)
    move_to_collection(arrow, presentation_collection)
    add_text("Presentation label | entry", "A   IN", (-1.72, -0.30, -1.14), 0.20, plum, presentation_collection)
    add_text("Presentation label | exit", "B   OUT", (1.72, -0.30, -1.14), 0.20, plum, presentation_collection)

    camera_data = bpy.data.cameras.new("Portal pair presentation camera")
    camera = bpy.data.objects.new("Portal pair presentation camera", camera_data)
    studio_collection.objects.link(camera)
    camera.location = (0.0, -8.5, 8.0)
    camera.rotation_euler = (Vector((0.0, 0.0, -0.08)) - camera.location).to_track_quat("-Z", "Y").to_euler()
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = 6.5
    scene.camera = camera

    add_area_light("Soft key", (-3.0, -5.5, 5.0), 780.0, 4.0, studio_collection)
    add_area_light("Cool fill", (4.0, -3.5, 1.3), 460.0, 3.5, studio_collection)
    add_area_light("Pink rim", (0.0, 1.5, 4.0), 520.0, 3.0, studio_collection)

    world = bpy.data.worlds.new("Cotton candy studio") if not bpy.data.worlds else bpy.data.worlds[0]
    scene.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.62, 0.36, 0.54, 1.0)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.8

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

    entry.select_set(False)
    exit_obj.select_set(True)
    bpy.context.view_layer.objects.active = exit_obj


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
    asset_collection = bpy.data.collections.new("PORTALS | editable endpoint meshes")
    presentation_collection = bpy.data.collections.new("PRESENTATION ONLY | flow cue and labels")
    studio_collection = bpy.data.collections.new("STUDIO | camera and lights")
    scene.collection.children.link(asset_collection)
    scene.collection.children.link(presentation_collection)
    scene.collection.children.link(studio_collection)

    global plum, dark_plum, cream, aperture
    plum = make_material("Deep plum | soft frame", (0.105, 0.045, 0.17), roughness=0.43)
    dark_plum = make_material("Aperture shadow | violet", (0.075, 0.035, 0.14), roughness=0.26, metallic=0.1)
    cream = make_material("Vanilla glow", (1.0, 0.91, 0.73), roughness=0.27, emission=0.12)
    aperture = make_material("Portal interior | grape", (0.20, 0.10, 0.31), roughness=0.28, metallic=0.12)
    entry_pink = make_material("Entry | strawberry pink", (0.96, 0.12, 0.39), roughness=0.28, metallic=0.12, emission=0.08)
    entry_glow = make_material("Entry | soft blush light", (1.0, 0.52, 0.69), roughness=0.25, emission=0.16)
    exit_cyan = make_material("Exit | candy cyan", (0.035, 0.72, 0.78), roughness=0.27, metallic=0.12, emission=0.08)
    exit_glow = make_material("Exit | mint light", (0.48, 1.0, 0.89), roughness=0.24, emission=0.16)

    entry = build_endpoint("Portal_Entry_Visual", -1.72, entry_pink, entry_glow, asset_collection)
    exit_obj = build_endpoint("Portal_Exit_Visual", 1.72, exit_cyan, exit_glow, asset_collection)
    setup_studio(entry, exit_obj, presentation_collection, studio_collection)

    MODEL_DIR.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_PATH))
    bpy.ops.render.render(write_still=True)

    export_fbx(ENTRY_FBX, [entry], reset_locations=True)
    export_fbx(EXIT_FBX, [exit_obj], reset_locations=True)
    export_fbx(PAIR_FBX, [entry, exit_obj], reset_locations=False)

    for obj in (entry, exit_obj):
        print("PORTAL_MESH_STATS", obj.name, "verts=", len(obj.data.vertices), "faces=", len(obj.data.polygons), "tris=", sum(max(0, len(poly.vertices) - 2) for poly in obj.data.polygons))


if __name__ == "__main__":
    build_scene()
