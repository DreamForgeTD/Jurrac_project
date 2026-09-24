"""Create a simple, smooth toy cannon for the Unity project."""

import math
from pathlib import Path

import bpy
import numpy as np
from mathutils import Vector


ROOT = Path(__file__).resolve().parents[1]
BLENDER_OUTPUT = Path(__file__).resolve().parent
UNITY_MODEL_DIR = ROOT / "Assets/Project/Resoruce_game/Model"
BLEND_PATH = BLENDER_OUTPUT / "CuteCannon.blend"
FBX_PATH = UNITY_MODEL_DIR / "CuteCannon.fbx"
TEXTURE_PATH = UNITY_MODEL_DIR / "CuteCannon_Albedo.png"
PREVIEW_PATH = BLENDER_OUTPUT / "CuteCannon_Preview.png"

TEXTURE_SIZE = 512
ATLAS_GRID = 4
TILE_MARGIN = 0.035
PALETTE = (
    "66CCB9", "F1846F", "F5C66B", "FFF1D8",
    "98D9CE", "263B4A", "F2A3A8", "A8DDE8",
    "9CC7A3", "F2B995", "D6A766", "FCF8EE",
    "8571A8", "679881", "6C9CC0", "192A36",
)


def hex_to_srgb(value):
    return np.array([int(value[i:i + 2], 16) / 255.0 for i in (0, 2, 4)], dtype=np.float32)


def srgb_to_linear(values):
    values = np.asarray(values, dtype=np.float32)
    return np.where(values <= 0.04045, values / 12.92, ((values + 0.055) / 1.055) ** 2.4)


def make_texture():
    rng = np.random.default_rng(12)
    tile_size = TEXTURE_SIZE // ATLAS_GRID
    pixels = np.ones((TEXTURE_SIZE, TEXTURE_SIZE, 4), dtype=np.float32)
    y, x = np.mgrid[0:tile_size, 0:tile_size]
    vertical = (y / max(tile_size - 1, 1) - 0.5)[..., None]
    horizontal = (x / max(tile_size - 1, 1) - 0.5)[..., None]

    for index, color_hex in enumerate(PALETTE):
        col, row = index % ATLAS_GRID, index // ATLAS_GRID
        x0, y0 = col * tile_size, row * tile_size
        base = hex_to_srgb(color_hex)[None, None, :]
        grain = rng.normal(0.0, 0.003, (tile_size, tile_size, 1)).astype(np.float32)
        gentle_shade = 1.0 - 0.03 * vertical + 0.012 * horizontal + grain
        tile = srgb_to_linear(np.clip(base * gentle_shade, 0.0, 1.0))
        pixels[y0:y0 + tile_size, x0:x0 + tile_size, :3] = tile

    image = bpy.data.images.new("Cute Cannon | Simple Colour Atlas", TEXTURE_SIZE, TEXTURE_SIZE, alpha=False)
    image.colorspace_settings.name = "sRGB"
    image.pixels.foreach_set(pixels.ravel())
    image.update()
    image.filepath_raw = str(TEXTURE_PATH)
    image.file_format = "PNG"
    image.save()
    image.pack()
    return image


def make_material(image):
    material = bpy.data.materials.new("Cute Cannon | Smooth Pastel")
    material.diffuse_color = (0.37, 0.72, 0.66, 1.0)
    material.use_nodes = True
    nodes = material.node_tree.nodes
    nodes.clear()

    texture = nodes.new("ShaderNodeTexImage")
    texture.label = "512 px pastel colour atlas"
    texture.image = image
    texture.interpolation = "Linear"
    shader = nodes.new("ShaderNodeBsdfPrincipled")
    shader.inputs["Roughness"].default_value = 0.62
    shader.inputs["Metallic"].default_value = 0.02
    output = nodes.new("ShaderNodeOutputMaterial")
    material.node_tree.links.new(texture.outputs["Color"], shader.inputs["Base Color"])
    material.node_tree.links.new(shader.outputs["BSDF"], output.inputs["Surface"])
    return material


def apply_modifier(obj, modifier):
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.modifier_apply(modifier=modifier.name)


def tile_uv(tile, u, v):
    tile_size = 1.0 / ATLAS_GRID
    col, row = tile % ATLAS_GRID, tile // ATLAS_GRID
    usable = tile_size - 2.0 * TILE_MARGIN
    return (
        col * tile_size + TILE_MARGIN + u * usable,
        row * tile_size + TILE_MARGIN + v * usable,
    )


def unwrap_into_tile(obj, tile):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(island_margin=0.025, area_weight=0.0, correct_aspect=True, scale_to_bounds=True)
    bpy.ops.object.mode_set(mode="OBJECT")
    layer = obj.data.uv_layers.active
    layer.name = "Cannon_Colour_Atlas_UV"

    us = [loop.uv.x for loop in layer.data]
    vs = [loop.uv.y for loop in layer.data]
    min_u, max_u = min(us), max(us)
    min_v, max_v = min(vs), max(vs)
    span_u = max(max_u - min_u, 1e-6)
    span_v = max(max_v - min_v, 1e-6)
    for loop in layer.data:
        local_u = (loop.uv.x - min_u) / span_u
        local_v = (loop.uv.y - min_v) / span_v
        loop.uv = tile_uv(tile, local_u, local_v)


def make_body(material):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0.0, 0.0, -0.055))
    body = bpy.context.object
    body.name = "Body | Soft rounded shell"
    body.data.name = "Body mesh"
    body.dimensions = (0.92, 0.62, 0.48)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)

    bevel = body.modifiers.new("Soft toy edges", "BEVEL")
    bevel.width = 0.145
    bevel.segments = 4
    bevel.limit_method = "ANGLE"
    if hasattr(bevel, "harden_normals"):
        bevel.harden_normals = True
    apply_modifier(body, bevel)

    for polygon in body.data.polygons:
        polygon.use_smooth = True
    normal = body.modifiers.new("Weighted smooth normals", "WEIGHTED_NORMAL")
    normal.keep_sharp = True
    normal.weight = 40
    apply_modifier(body, normal)

    body.data.materials.append(material)
    unwrap_into_tile(body, 0)
    return body


def make_barrel(material):
    # The cannon aims along local +Z to match the project's existing Cannon prefab.
    sides = 16
    ring_specs = (
        (0.02, 0.185),
        (0.78, 0.205),
        (0.94, 0.245),
        (0.94, 0.162),
        (0.58, 0.162),
    )
    vertices = []
    ring_indices = []
    for z, radius in ring_specs:
        ring = []
        for index in range(sides):
            angle = 2.0 * math.pi * index / sides
            ring.append(len(vertices))
            vertices.append((radius * math.cos(angle), radius * math.sin(angle), z))
        ring_indices.append(ring)
    bore_center = len(vertices)
    vertices.append((0.0, 0.0, ring_specs[-1][0]))

    faces = []
    face_uvs = []
    smooth_faces = []

    def add_quad(ring_a, ring_b, tile, v_a, v_b, reverse=False):
        for index in range(sides):
            nxt = (index + 1) % sides
            if reverse:
                face = (ring_a[index], ring_a[nxt], ring_b[nxt], ring_b[index])
            else:
                face = (ring_a[index], ring_b[index], ring_b[nxt], ring_a[nxt])
            faces.append(face)
            u0, u1 = index / sides, (index + 1) / sides
            if reverse:
                local_uvs = ((u0, v_a), (u1, v_a), (u1, v_b), (u0, v_b))
            else:
                local_uvs = ((u0, v_a), (u0, v_b), (u1, v_b), (u1, v_a))
            face_uvs.append((tile, local_uvs))
            smooth_faces.append(True)

    # Coral outer tube and a small integrated flared muzzle.
    add_quad(ring_indices[0], ring_indices[1], 1, 0.04, 0.82)
    add_quad(ring_indices[1], ring_indices[2], 1, 0.82, 0.98)

    # A warm muzzle lip and dark inner bore are UV-painted parts of the barrel mesh.
    outer_ring, inner_ring = ring_indices[2], ring_indices[3]
    for index in range(sides):
        nxt = (index + 1) % sides
        faces.append((outer_ring[index], outer_ring[nxt], inner_ring[nxt], inner_ring[index]))
        annulus_uvs = []
        for vertex_index in (outer_ring[index], outer_ring[nxt], inner_ring[nxt], inner_ring[index]):
            x, y, _z = vertices[vertex_index]
            annulus_uvs.append((0.5 + x / (2.0 * ring_specs[2][1]), 0.5 + y / (2.0 * ring_specs[2][1])))
        face_uvs.append((2, tuple(annulus_uvs)))
        smooth_faces.append(False)

    add_quad(ring_indices[3], ring_indices[4], 5, 0.98, 0.04, reverse=True)
    cap_ring = ring_indices[4]
    cap_face = (bore_center, *cap_ring)
    faces.append(cap_face)
    cap_uvs = [(0.5, 0.5)]
    cap_radius = ring_specs[4][1]
    for vertex_index in cap_ring:
        x, y, _z = vertices[vertex_index]
        cap_uvs.append((0.5 + x / (2.0 * cap_radius), 0.5 + y / (2.0 * cap_radius)))
    face_uvs.append((5, tuple(cap_uvs)))
    smooth_faces.append(False)

    mesh = bpy.data.meshes.new("Barrel mesh | hollow and tapered")
    mesh.from_pydata(vertices, [], faces)
    mesh.materials.append(material)
    mesh.update()
    uv_layer = mesh.uv_layers.new(name="Cannon_Colour_Atlas_UV")
    for polygon, (tile, coords), smooth in zip(mesh.polygons, face_uvs, smooth_faces):
        polygon.use_smooth = smooth
        for loop_index, local_uv in zip(polygon.loop_indices, coords):
            uv_layer.data[loop_index].uv = tile_uv(tile, *local_uv)

    barrel = bpy.data.objects.new("Barrel | Tapered hollow tube", mesh)
    bpy.context.collection.objects.link(barrel)
    bpy.context.view_layer.objects.active = barrel
    barrel.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.normals_make_consistent(inside=False)
    bpy.ops.object.mode_set(mode="OBJECT")
    return barrel


def add_area_light(name, location, energy, size, target=(0.0, 0.0, 0.25)):
    data = bpy.data.lights.new(name, "AREA")
    data.energy = energy
    data.shape = "DISK"
    data.size = size
    light = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(light)
    light.location = location
    light.rotation_euler = (Vector(target) - light.location).to_track_quat("-Z", "Y").to_euler()
    return light


def render_preview():
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE" if "BLENDER_EEVEE" in {i.identifier for i in scene.render.bl_rna.properties["engine"].enum_items} else "BLENDER_EEVEE_NEXT"
    scene.render.resolution_x = 900
    scene.render.resolution_y = 900
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGB"
    scene.render.filepath = str(PREVIEW_PATH)
    scene.view_settings.view_transform = "AgX"
    scene.view_settings.look = "AgX - Medium High Contrast"

    world = bpy.data.worlds.new("Simple Cannon Preview World") if not bpy.data.worlds else bpy.data.worlds[0]
    scene.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.78, 0.86, 0.84, 1.0)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.65

    ground_mat = bpy.data.materials.new("Preview ground | warm cream")
    ground_mat.diffuse_color = (0.92, 0.87, 0.79, 1.0)
    ground_mat.use_nodes = True
    ground_mat.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.92, 0.87, 0.79, 1.0)
    ground_mat.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 0.8
    bpy.ops.mesh.primitive_plane_add(size=200.0, location=(0.0, 0.0, -0.30))
    ground = bpy.context.object
    ground.name = "Temporary preview ground"
    ground.data.materials.append(ground_mat)

    camera_data = bpy.data.cameras.new("Temporary preview camera")
    camera = bpy.data.objects.new("Temporary preview camera", camera_data)
    bpy.context.collection.objects.link(camera)
    camera.location = (2.2, -5.4, 2.2)
    camera.rotation_euler = (Vector((0.0, 0.0, 0.28)) - camera.location).to_track_quat("-Z", "Y").to_euler()
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = 2.75
    scene.camera = camera

    objects = [ground, camera]
    objects.append(add_area_light("Temporary key light", (-3.2, -4.0, 4.5), 520.0, 3.5))
    objects.append(add_area_light("Temporary fill light", (3.8, -1.8, 2.1), 300.0, 3.0))
    objects.append(add_area_light("Temporary rim light", (0.0, 3.0, 3.5), 340.0, 2.5))
    bpy.ops.render.render(write_still=True)

    for obj in objects:
        bpy.data.objects.remove(obj, do_unlink=True)
    scene.camera = None
    bpy.data.materials.remove(ground_mat)


def export_fbx(model_objects):
    bpy.ops.object.select_all(action="DESELECT")
    for obj in model_objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = model_objects[0]
    bpy.ops.export_scene.fbx(
        filepath=str(FBX_PATH),
        use_selection=True,
        object_types={"MESH"},
        apply_scale_options="FBX_SCALE_ALL",
        use_mesh_modifiers=True,
        mesh_smooth_type="FACE",
        use_subsurf=False,
        use_mesh_edges=False,
        use_tspace=False,
        add_leaf_bones=False,
        bake_anim=False,
        path_mode="RELATIVE",
        embed_textures=False,
        axis_forward="-Z",
        axis_up="Y",
        use_custom_props=True,
    )


def configure_edit_view(model_objects):
    bpy.ops.object.select_all(action="DESELECT")
    for obj in model_objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = model_objects[0]
    look_direction = (Vector((2.2, -5.4, 2.2)) - Vector((0.0, 0.0, 0.28))).to_track_quat("-Z", "Y")
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type == "VIEW_3D":
                space = area.spaces.active
                space.region_3d.view_perspective = "ORTHO"
                space.region_3d.view_location = Vector((0.0, 0.0, 0.35))
                space.region_3d.view_distance = 2.8
                space.region_3d.view_rotation = look_direction
                space.shading.type = "MATERIAL"
                if hasattr(space.overlay, "show_floor"):
                    space.overlay.show_floor = False
                if hasattr(space.overlay, "show_extras"):
                    space.overlay.show_extras = False


def main():
    UNITY_MODEL_DIR.mkdir(parents=True, exist_ok=True)
    bpy.context.preferences.filepaths.save_version = 0
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    for image in list(bpy.data.images):
        if image.users == 0:
            bpy.data.images.remove(image)
    for material in list(bpy.data.materials):
        if material.users == 0:
            bpy.data.materials.remove(material)

    image = make_texture()
    material = make_material(image)
    body = make_body(material)
    barrel = make_barrel(material)
    model_objects = [body, barrel]

    render_preview()
    export_fbx(model_objects)
    bpy.ops.file.pack_all()
    configure_edit_view(model_objects)
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_PATH))

    triangles = sum(sum(len(face.vertices) - 2 for face in obj.data.polygons) for obj in model_objects)
    vertices = sum(len(obj.data.vertices) for obj in model_objects)
    print(f"Simple Cannon complete: {len(model_objects)} editable parts, {vertices} verts, {triangles} tris")
    print(f"Blend: {BLEND_PATH}")
    print(f"FBX: {FBX_PATH}")
    print(f"Texture: {TEXTURE_PATH}")
    print(f"Preview: {PREVIEW_PATH}")


if __name__ == "__main__":
    main()
