"""Build a small, smooth, two-pole horseshoe magnet."""

import math
from pathlib import Path

import bpy
from mathutils import Vector


ROOT = Path(__file__).resolve().parents[1]
OUTPUT_DIR = Path(__file__).resolve().parent
MODEL_DIR = ROOT / "Assets/Project/Resoruce_game/Model"
BLEND_PATH = OUTPUT_DIR / "HorseshoeMagnet_Thick.blend"
FBX_PATH = MODEL_DIR / "HorseshoeMagnet_Thick.fbx"
PREVIEW_PATH = OUTPUT_DIR / "HorseshoeMagnet_Thick_Preview.png"


def srgb_to_linear_component(value):
    value /= 255.0
    if value <= 0.04045:
        return value / 12.92
    return ((value + 0.055) / 1.055) ** 2.4


def color_from_hex(value):
    return tuple(srgb_to_linear_component(int(value[index:index + 2], 16)) for index in (0, 2, 4)) + (1.0,)


def make_material(name, hex_color):
    color = color_from_hex(hex_color)
    material = bpy.data.materials.new(name)
    material.diffuse_color = color
    material.use_nodes = True
    shader = material.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = color
    shader.inputs["Roughness"].default_value = 0.42
    shader.inputs["Metallic"].default_value = 0.08
    return material


def build_magnet(red, blue):
    arm_radius = 0.62
    bend_center_z = 0.18
    path = [(-arm_radius, 0.0, 0.94), (-arm_radius, 0.0, bend_center_z)]
    bend_segments = 12
    for step in range(1, bend_segments + 1):
        angle = math.pi + math.pi * step / bend_segments
        path.append((arm_radius * math.cos(angle), 0.0, bend_center_z + arm_radius * math.sin(angle)))
    path.append((arm_radius, 0.0, 0.94))

    cross_section_sides = 8
    tube_radius = 0.22
    vertices = []
    rings = []
    for path_index, point in enumerate(path):
        previous = path[max(path_index - 1, 0)]
        following = path[min(path_index + 1, len(path) - 1)]
        tangent_x = following[0] - previous[0]
        tangent_z = following[2] - previous[2]
        tangent_length = math.hypot(tangent_x, tangent_z)
        tangent_x /= tangent_length
        tangent_z /= tangent_length
        normal_x, normal_z = -tangent_z, tangent_x

        ring = []
        for side in range(cross_section_sides):
            angle = 2.0 * math.pi * side / cross_section_sides
            planar_offset = tube_radius * math.cos(angle)
            depth_offset = tube_radius * math.sin(angle)
            ring.append(len(vertices))
            vertices.append((
                point[0] + normal_x * planar_offset,
                point[1] + depth_offset,
                point[2] + normal_z * planar_offset,
            ))
        rings.append(ring)

    faces = []
    face_materials = []
    smooth_faces = []
    for path_index in range(len(path) - 1):
        average_x = (path[path_index][0] + path[path_index + 1][0]) * 0.5
        material_index = 0 if average_x < 0.0 else 1
        for side in range(cross_section_sides):
            next_side = (side + 1) % cross_section_sides
            faces.append((
                rings[path_index][side],
                rings[path_index][next_side],
                rings[path_index + 1][next_side],
                rings[path_index + 1][side],
            ))
            face_materials.append(material_index)
            smooth_faces.append(True)

    # Flat end faces make the red and blue poles read clearly.
    faces.append(tuple(rings[0]))
    face_materials.append(0)
    smooth_faces.append(False)
    faces.append(tuple(reversed(rings[-1])))
    face_materials.append(1)
    smooth_faces.append(False)

    mesh = bpy.data.meshes.new("Horseshoe magnet | low poly mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.materials.append(red)
    mesh.materials.append(blue)
    mesh.update()

    for polygon, material_index, smooth in zip(mesh.polygons, face_materials, smooth_faces):
        polygon.material_index = material_index
        polygon.use_smooth = smooth

    magnet = bpy.data.objects.new("U Magnet | Red and Blue Poles", mesh)
    bpy.context.collection.objects.link(magnet)
    bpy.context.view_layer.objects.active = magnet
    magnet.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.normals_make_consistent(inside=False)
    bpy.ops.object.mode_set(mode="OBJECT")

    magnet["North Pole"] = "Red"
    magnet["South Pole"] = "Blue"
    magnet["Low Poly"] = True
    magnet["Triangles"] = sum(len(face.vertices) - 2 for face in mesh.polygons)
    return magnet


def add_area_light(name, location, energy, size, target):
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
    engines = {item.identifier for item in scene.render.bl_rna.properties["engine"].enum_items}
    scene.render.engine = "BLENDER_EEVEE_NEXT" if "BLENDER_EEVEE_NEXT" in engines else "BLENDER_EEVEE"
    scene.render.resolution_x = 900
    scene.render.resolution_y = 900
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGB"
    scene.render.filepath = str(PREVIEW_PATH)
    scene.view_settings.view_transform = "AgX"
    scene.view_settings.look = "AgX - Medium High Contrast"

    world = bpy.data.worlds.new("Magnet Preview World") if not bpy.data.worlds else bpy.data.worlds[0]
    scene.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.79, 0.84, 0.88, 1.0)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.65

    ground_material = bpy.data.materials.new("Temporary preview ground")
    ground_material.diffuse_color = (0.91, 0.88, 0.82, 1.0)
    ground_material.use_nodes = True
    ground_material.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.91, 0.88, 0.82, 1.0)

    bpy.ops.mesh.primitive_plane_add(size=200.0, location=(0.0, 0.0, -0.59))
    ground = bpy.context.object
    ground.name = "Temporary preview ground"
    ground.data.materials.append(ground_material)

    camera_data = bpy.data.cameras.new("Temporary magnet preview camera")
    camera = bpy.data.objects.new("Temporary magnet preview camera", camera_data)
    bpy.context.collection.objects.link(camera)
    camera.location = (2.2, -5.5, 1.55)
    camera.rotation_euler = (Vector((0.0, 0.0, 0.18)) - camera.location).to_track_quat("-Z", "Y").to_euler()
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = 2.35
    scene.camera = camera

    temporary_objects = [ground, camera]
    temporary_objects.append(add_area_light("Temporary key light", (-3.5, -4.0, 4.0), 520.0, 3.5, (0.0, 0.0, 0.15)))
    temporary_objects.append(add_area_light("Temporary fill light", (4.0, -2.0, 2.0), 300.0, 3.0, (0.0, 0.0, 0.15)))
    temporary_objects.append(add_area_light("Temporary rim light", (0.0, 3.0, 3.5), 360.0, 3.0, (0.0, 0.0, 0.15)))
    bpy.ops.render.render(write_still=True)

    for obj in temporary_objects:
        bpy.data.objects.remove(obj, do_unlink=True)
    scene.camera = None
    bpy.data.materials.remove(ground_material)


def configure_edit_view(magnet):
    bpy.ops.object.select_all(action="DESELECT")
    magnet.select_set(True)
    bpy.context.view_layer.objects.active = magnet
    view_direction = (Vector((2.2, -5.5, 1.55)) - Vector((0.0, 0.0, 0.18))).to_track_quat("-Z", "Y")
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type == "VIEW_3D":
                space = area.spaces.active
                space.region_3d.view_perspective = "ORTHO"
                space.region_3d.view_location = Vector((0.0, 0.0, 0.16))
                space.region_3d.view_distance = 2.5
                space.region_3d.view_rotation = view_direction
                space.shading.type = "MATERIAL"
                if hasattr(space.overlay, "show_floor"):
                    space.overlay.show_floor = False
                if hasattr(space.overlay, "show_extras"):
                    space.overlay.show_extras = False


def export_fbx(magnet):
    bpy.ops.object.select_all(action="DESELECT")
    magnet.select_set(True)
    bpy.context.view_layer.objects.active = magnet
    bpy.ops.export_scene.fbx(
        filepath=str(FBX_PATH),
        use_selection=True,
        object_types={"MESH"},
        apply_scale_options="FBX_SCALE_ALL",
        use_mesh_modifiers=True,
        mesh_smooth_type="FACE",
        use_subsurf=False,
        use_mesh_edges=False,
        add_leaf_bones=False,
        bake_anim=False,
        path_mode="AUTO",
        axis_forward="-Z",
        axis_up="Y",
        use_custom_props=True,
    )


def main():
    MODEL_DIR.mkdir(parents=True, exist_ok=True)
    bpy.context.preferences.filepaths.save_version = 0
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    for material in list(bpy.data.materials):
        if material.users == 0:
            bpy.data.materials.remove(material)

    red = make_material("North Pole | Red", "EF4657")
    blue = make_material("South Pole | Blue", "3475E5")
    magnet = build_magnet(red, blue)

    render_preview()
    export_fbx(magnet)
    configure_edit_view(magnet)
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_PATH))

    print(f"Horseshoe magnet complete: {len(magnet.data.vertices)} verts, {magnet['Triangles']} tris")
    print(f"Blend: {BLEND_PATH}")
    print(f"FBX: {FBX_PATH}")
    print(f"Preview: {PREVIEW_PATH}")


if __name__ == "__main__":
    main()
