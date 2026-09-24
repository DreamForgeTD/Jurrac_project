import math
from pathlib import Path

import bpy
from mathutils import Vector


OUTPUT_DIR = Path(__file__).resolve().parent
BLEND_PATH = OUTPUT_DIR / "Target.blend"
TEXTURE_PATH = OUTPUT_DIR / "Target_texture.png"
PREVIEW_PATH = OUTPUT_DIR / "Target_preview.png"
SEGMENTS = 64
RADIUS = 2.8
THICKNESS = 0.2


def make_target_texture():
    import numpy as np

    size = 1024
    samples_per_axis = 2
    target_radius = size * 0.48
    center = size / 2.0
    x = np.arange(size, dtype=np.float32)[None, :] + 0.5
    y = np.arange(size, dtype=np.float32)[:, None] + 0.5
    red = np.array((0.72, 0.008, 0.018), dtype=np.float32)
    white = np.array((0.91, 0.91, 0.88), dtype=np.float32)
    rim = np.array((0.045, 0.055, 0.065), dtype=np.float32)
    covered_samples = np.zeros((size, size), dtype=np.float32)
    accumulated_rgb = np.zeros((size, size, 3), dtype=np.float32)

    sample_offsets = (0.25, 0.75)
    for offset_y in sample_offsets:
        for offset_x in sample_offsets:
            distance = np.hypot(x + offset_x - center, y + offset_y - center)
            on_disc = distance < size * 0.5
            inside_target = distance < target_radius
            zone = (distance / (target_radius / 5.0)).astype(np.int32)
            red_pixels = inside_target & (zone % 2 == 0)
            white_pixels = inside_target & (zone % 2 == 1)
            rim_pixels = on_disc & ~inside_target
            covered_samples += on_disc
            accumulated_rgb[red_pixels] += red
            accumulated_rgb[white_pixels] += white
            accumulated_rgb[rim_pixels] += rim

    rgba = np.zeros((size, size, 4), dtype=np.float32)
    rgba[..., :3] = accumulated_rgb / np.maximum(covered_samples[..., None], 1.0)
    rgba[..., 3] = 1.0

    image = bpy.data.images.new("Target_Rings_Texture", width=size, height=size, alpha=False)
    image.colorspace_settings.name = "sRGB"
    image.pixels.foreach_set(rgba.ravel())
    image.update()
    image.filepath_raw = str(TEXTURE_PATH)
    image.file_format = "PNG"
    image.save()
    image.pack()
    return image


def make_front_material(image):
    material = bpy.data.materials.new("Target face | image texture")
    material.diffuse_color = (0.72, 0.008, 0.018, 1.0)
    material.use_nodes = True
    nodes = material.node_tree.nodes
    nodes.clear()

    texture = nodes.new("ShaderNodeTexImage")
    texture.name = "Target rings image"
    texture.label = "1024 px target texture"
    texture.image = image
    texture.interpolation = "Linear"

    emission = nodes.new("ShaderNodeEmission")
    output = nodes.new("ShaderNodeOutputMaterial")
    links = material.node_tree.links
    links.new(texture.outputs["Color"], emission.inputs["Color"])
    links.new(emission.outputs["Emission"], output.inputs["Surface"])
    return material


def make_side_material():
    material = bpy.data.materials.new("Target edge | charcoal")
    material.diffuse_color = (0.045, 0.055, 0.065, 1.0)
    material.use_nodes = True
    shader = material.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = (0.045, 0.055, 0.065, 1.0)
    shader.inputs["Metallic"].default_value = 0.22
    shader.inputs["Roughness"].default_value = 0.38
    return material


def make_target_mesh(front_material, side_material):
    top_z = THICKNESS / 2.0
    bottom_z = -THICKNESS / 2.0
    vertices = [(0.0, 0.0, top_z)]
    front_ring = []
    angles = [2.0 * math.pi * i / SEGMENTS for i in range(SEGMENTS)]
    for angle in angles:
        front_ring.append(len(vertices))
        vertices.append((RADIUS * math.cos(angle), RADIUS * math.sin(angle), top_z))

    back_center = len(vertices)
    vertices.append((0.0, 0.0, bottom_z))
    back_ring = []
    for angle in angles:
        back_ring.append(len(vertices))
        vertices.append((RADIUS * math.cos(angle), RADIUS * math.sin(angle), bottom_z))

    faces = []
    material_indices = []
    for index in range(SEGMENTS):
        next_index = (index + 1) % SEGMENTS
        faces.append((0, front_ring[index], front_ring[next_index]))
        material_indices.append(0)

    for index in range(SEGMENTS):
        next_index = (index + 1) % SEGMENTS
        faces.append((front_ring[index], back_ring[index], back_ring[next_index], front_ring[next_index]))
        material_indices.append(1)

    faces.append((back_center, *reversed(back_ring)))
    material_indices.append(1)

    mesh = bpy.data.meshes.new("64-sided disk | 130 verts, 129 faces")
    mesh.from_pydata(vertices, [], faces)
    mesh.materials.append(front_material)
    mesh.materials.append(side_material)
    mesh.update()

    for polygon, material_index in zip(mesh.polygons, material_indices):
        polygon.material_index = material_index
        polygon.use_smooth = material_index == 1 and len(polygon.vertices) == 4

    uv_layer = mesh.uv_layers.new(name="Target texture UV")
    for loop_index, loop in enumerate(mesh.loops):
        vertex_index = loop.vertex_index
        if vertex_index == 0:
            uv_layer.data[loop_index].uv = (0.5, 0.5)
        elif 1 <= vertex_index <= SEGMENTS:
            angle = angles[vertex_index - 1]
            uv_layer.data[loop_index].uv = (0.5 + 0.5 * math.cos(angle), 0.5 + 0.5 * math.sin(angle))
        else:
            uv_layer.data[loop_index].uv = (0.5, 0.5)

    target = bpy.data.objects.new("Target | textured 3D disk (64 sides)", mesh)
    bpy.context.collection.objects.link(target)

    bevel = target.modifiers.new("Small edge bevel", "BEVEL")
    bevel.width = 0.025
    bevel.segments = 2
    bevel.limit_method = "ANGLE"
    bevel.angle_limit = math.radians(30.0)
    if hasattr(bevel, "material"):
        bevel.material = 1
    return target


def add_area_light(name, location, energy, size):
    light_data = bpy.data.lights.new(name, "AREA")
    light_data.energy = energy
    light_data.shape = "DISK"
    light_data.size = size
    light = bpy.data.objects.new(name, light_data)
    bpy.context.collection.objects.link(light)
    light.location = location
    return light


def setup_scene(target):
    scene = bpy.context.scene
    camera_data = bpy.data.cameras.new("Slightly raised target view")
    camera = bpy.data.objects.new("Slightly raised target view", camera_data)
    bpy.context.collection.objects.link(camera)
    camera.location = (0.0, -3.6, 10.5)
    camera.rotation_euler = (Vector((0.0, 0.0, 0.0)) - camera.location).to_track_quat("-Z", "Y").to_euler()
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = 7.25
    scene.camera = camera

    add_area_light("Large softbox", (-3.2, -2.8, 6.0), 900.0, 4.0)
    add_area_light("Gentle fill", (3.0, 2.4, 5.5), 450.0, 5.0)

    world = bpy.data.worlds.new("Neutral background") if not bpy.data.worlds else bpy.data.worlds[0]
    scene.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.30, 0.33, 0.37, 1.0)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.8

    available_engines = {item.identifier for item in scene.render.bl_rna.properties["engine"].enum_items}
    scene.render.engine = "BLENDER_EEVEE_NEXT" if "BLENDER_EEVEE_NEXT" in available_engines else "BLENDER_EEVEE"
    scene.render.resolution_x = 1200
    scene.render.resolution_y = 1200
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
                space.region_3d.view_distance = 7.2
                space.region_3d.view_rotation = camera.rotation_euler.to_quaternion()
                space.shading.type = "MATERIAL"
                if hasattr(space.overlay, "show_floor"):
                    space.overlay.show_floor = False
                if hasattr(space.overlay, "show_extras"):
                    space.overlay.show_extras = False

    bpy.ops.object.select_all(action="DESELECT")
    target.select_set(True)
    bpy.context.view_layer.objects.active = target


def build_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for image in list(bpy.data.images):
        if image.users == 0:
            bpy.data.images.remove(image)
    for material in list(bpy.data.materials):
        if material.users == 0:
            bpy.data.materials.remove(material)

    texture = make_target_texture()
    front_material = make_front_material(texture)
    side_material = make_side_material()
    target = make_target_mesh(front_material, side_material)
    setup_scene(target)

    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_PATH))
    bpy.ops.render.render(write_still=True)


if __name__ == "__main__":
    build_scene()
