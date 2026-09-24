"""Add a short, keyframed firing animation to PuzzleCannon.blend.

Creates a new animated .blend and MP4 preview beside the source model. No Unity
assets are read or written.
"""

import math
from pathlib import Path

import bpy
from mathutils import Vector


OUTPUT_DIR = Path(__file__).resolve().parent
ANIMATED_BLEND = OUTPUT_DIR / "PuzzleCannon_Firing_v3.blend"
ANIMATION_MP4 = OUTPUT_DIR / "PuzzleCannon_Firing_v3.mp4"
PREVIEW_PNG = OUTPUT_DIR / "PuzzleCannon_Firing_v3_preview.png"


def srgb_channel(value):
    value /= 255.0
    return value / 12.92 if value <= 0.04045 else ((value + 0.055) / 1.055) ** 2.4


def make_material(name, color_hex, roughness=0.35, emission=0.0):
    rgb = tuple(srgb_channel(int(color_hex[index:index + 2], 16)) for index in (0, 2, 4))
    material = bpy.data.materials.new(name)
    material.diffuse_color = (*rgb, 1.0)
    material.use_nodes = True
    shader = material.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = (*rgb, 1.0)
    shader.inputs["Roughness"].default_value = roughness
    if emission:
        if "Emission Color" in shader.inputs:
            shader.inputs["Emission Color"].default_value = (*rgb, 1.0)
        else:
            shader.inputs["Emission"].default_value = (*rgb, 1.0)
        shader.inputs["Emission Strength"].default_value = emission
    return material


def link_fx(obj, collection):
    for old_collection in list(obj.users_collection):
        old_collection.objects.unlink(obj)
    collection.objects.link(obj)
    return obj


def add_sphere(name, radius, location, material, collection, segments=12, rings=8):
    bpy.ops.mesh.primitive_uv_sphere_add(
        segments=segments, ring_count=rings, radius=radius, location=location
    )
    obj = bpy.context.object
    obj.name = name
    obj.data.name = f"{name} mesh"
    obj.data.materials.append(material)
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
    return link_fx(obj, collection)


def add_torus(name, major_radius, minor_radius, location, material, collection, axis="Y"):
    bpy.ops.mesh.primitive_torus_add(
        major_segments=20,
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
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
    return link_fx(obj, collection)


def add_starburst(name, radius, location, materials, collection, points=10):
    """A small 2D starburst facing the camera on the cannon's +X side."""
    vertices = [(0.0, 0.0, 0.0)]
    for index in range(points * 2):
        angle = 2.0 * math.pi * index / (points * 2)
        radial = radius if index % 2 == 0 else radius * 0.60
        vertices.append((0.0, radial * math.cos(angle), radial * math.sin(angle)))
    faces = []
    for index in range(points * 2):
        faces.append((0, index + 1, ((index + 1) % (points * 2)) + 1))
    mesh = bpy.data.meshes.new(f"{name} mesh")
    mesh.from_pydata(vertices, [], faces)
    for material in materials:
        mesh.materials.append(material)
    for index, polygon in enumerate(mesh.polygons):
        polygon.material_index = index % len(materials)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    collection.objects.link(obj)
    obj.location = location
    return obj


def parent_keep_transform(obj, parent):
    world = obj.matrix_world.copy()
    obj.parent = parent
    obj.matrix_parent_inverse = parent.matrix_world.inverted()
    obj.matrix_world = world


def key_transform(obj, frame, location=None, scale=None, rotation=None):
    if location is not None:
        obj.location = location
        obj.keyframe_insert(data_path="location", frame=frame)
    if scale is not None:
        obj.scale = scale
        obj.keyframe_insert(data_path="scale", frame=frame)
    if rotation is not None:
        obj.rotation_euler = rotation
        obj.keyframe_insert(data_path="rotation_euler", frame=frame)


def set_camera(scene):
    camera = bpy.data.objects.get("STUDIO | Camera")
    if camera is None:
        raise RuntimeError("Could not find the studio camera in PuzzleCannon.blend")
    camera.location = (5.7, 2.2, 2.9)
    target = Vector((0.0, 2.0, 0.88))
    base_rotation = (target - camera.location).to_track_quat("-Z", "Y").to_euler()
    camera.rotation_euler = base_rotation
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = 6.35
    scene.camera = camera

    # A tiny view shake accompanies the recoil without obscuring the action.
    camera.animation_data_clear()
    camera_base = camera.location.copy()
    rotation_base = camera.rotation_euler.copy()
    for frame, offset, rotation_delta in (
        (1, (0.0, 0.0, 0.0), (0.0, 0.0, 0.0)),
        (8, (0.0, 0.0, 0.0), (0.0, 0.0, 0.0)),
        (9, (0.04, -0.015, 0.015), (0.006, -0.004, 0.010)),
        (10, (-0.035, 0.02, -0.012), (-0.005, 0.006, -0.008)),
        (11, (0.024, -0.012, 0.008), (0.004, -0.004, 0.006)),
        (13, (-0.012, 0.008, -0.004), (-0.002, 0.002, -0.003)),
        (16, (0.0, 0.0, 0.0), (0.0, 0.0, 0.0)),
        (42, (0.0, 0.0, 0.0), (0.0, 0.0, 0.0)),
    ):
        camera.location = camera_base + Vector(offset)
        camera.rotation_euler = tuple(rotation_base[i] + rotation_delta[i] for i in range(3))
        camera.keyframe_insert(data_path="location", frame=frame)
        camera.keyframe_insert(data_path="rotation_euler", frame=frame)
    camera.location = camera_base
    camera.rotation_euler = rotation_base
    return camera


def animate_cannon(scene, model_collection):
    model_objects = [
        bpy.data.objects[name]
        for name in (
            "Carriage | body and trim",
            "Wheel | near side with puzzle dial",
            "Wheel | far side",
            "Barrel | complete hollow cannon assembly",
            "Elevation | cradle and bearings",
            "Trigger | charge lever",
        )
    ]
    root = bpy.data.objects.new("ANIM | Cannon recoil and side-to-side settle", None)
    root.empty_display_type = "CIRCLE"
    root.empty_display_size = 0.55
    root.location = (0.0, 0.0, 0.37)
    model_collection.objects.link(root)

    # Put the barrel's pivot on its centerline, so squash/stretch does not sink it.
    barrel = bpy.data.objects["Barrel | complete hollow cannon assembly"]
    bpy.ops.object.select_all(action="DESELECT")
    barrel.select_set(True)
    bpy.context.view_layer.objects.active = barrel
    bpy.context.scene.cursor.location = (0.0, 0.0, 0.925)
    bpy.ops.object.origin_set(type="ORIGIN_CURSOR")
    bpy.context.scene.cursor.location = (0.0, 0.0, 0.0)

    for obj in model_objects:
        parent_keep_transform(obj, root)

    recoil_keys = (
        (1, (0.0, 0.0, 0.37), (0.0, 0.0, 0.0)),
        (7, (0.0, 0.0, 0.37), (0.0, 0.0, 0.0)),
        (8, (0.0, 0.0, 0.37), (0.0, 0.0, 0.0)),
        (9, (0.010, -0.125, 0.37), (-0.025, 0.0, 0.018)),
        (10, (-0.020, -0.180, 0.37), (0.020, 0.0, -0.032)),
        (12, (0.023, -0.135, 0.37), (-0.012, 0.0, 0.027)),
        (15, (-0.017, -0.090, 0.37), (0.010, 0.0, -0.023)),
        (18, (0.013, -0.050, 0.37), (-0.007, 0.0, 0.016)),
        (22, (-0.007, -0.022, 0.37), (0.004, 0.0, -0.009)),
        (27, (0.002, -0.004, 0.37), (-0.001, 0.0, 0.002)),
        (31, (0.0, 0.0, 0.37), (0.0, 0.0, 0.0)),
        (42, (0.0, 0.0, 0.37), (0.0, 0.0, 0.0)),
    )
    for frame, location, rotation in recoil_keys:
        key_transform(root, frame, location=location, rotation=rotation)
    return root, barrel


def build_animation():
    source = OUTPUT_DIR / "PuzzleCannon.blend"
    if not source.exists():
        raise FileNotFoundError(f"Base model not found: {source}")
    if bpy.data.objects.get("Barrel | complete hollow cannon assembly") is None:
        raise RuntimeError("Run Blender with PuzzleCannon.blend loaded before this script")

    scene = bpy.context.scene
    model_collection = bpy.data.collections["PUZZLE CANNON | Editable Model"]
    studio_collection = bpy.data.collections["STUDIO | Preview only"]
    fx_collection = bpy.data.collections.new("ANIMATION | Projectile and cartoon FX")
    scene.collection.children.link(fx_collection)
    scene.frame_start = 1
    scene.frame_end = 42
    scene.render.fps = 24
    scene.render.resolution_x = 960
    scene.render.resolution_y = 540
    scene.render.resolution_percentage = 100
    scene.render.engine = "BLENDER_EEVEE" if "BLENDER_EEVEE" in {
        item.identifier for item in scene.render.bl_rna.properties["engine"].enum_items
    } else "BLENDER_EEVEE_NEXT"
    scene.view_settings.view_transform = "AgX"
    scene.view_settings.look = "AgX - Medium High Contrast"
    set_camera(scene)

    root, barrel = animate_cannon(scene, model_collection)

    # Pull the barrel back along local Y; inflate only across X/Z, then thrust
    # forward as the round is fired. Its Y scale remains fixed at 1 throughout.
    barrel.animation_data_clear()
    barrel_base_location = barrel.location.copy()
    barrel_shape_keys = (
        (1, 0.00, 1.00),
        (2, -0.07, 0.94),
        (4, -0.22, 0.86),
        (5, -0.32, 0.82),
        (6, -0.32, 0.98),
        (7, -0.32, 1.20),
        (8, -0.32, 1.42),
        (9, 0.04, 1.50),
        (10, 0.06, 1.28),
        (11, 0.02, 1.12),
        (12, 0.00, 1.00),
        (42, 0.00, 1.00),
    )
    for frame, y_offset, radial_scale in barrel_shape_keys:
        key_transform(
            barrel,
            frame,
            location=(barrel_base_location.x, barrel_base_location.y + y_offset, barrel_base_location.z),
            scale=(radial_scale, 1.0, radial_scale),
        )

    gold = make_material("FX | warm gold glow", "FFE36A", roughness=0.25, emission=1.2)
    coral = make_material("FX | pop coral", "F47768", roughness=0.28, emission=0.55)
    aqua = make_material("FX | bright aqua projectile", "62E2DC", roughness=0.22, emission=0.8)
    cream = make_material("FX | flash cream", "FFF5D7", roughness=0.23, emission=1.5)
    pink = make_material("FX | candy pink spark", "F28AB0", roughness=0.25, emission=0.6)
    smoke_mat = make_material("FX | soft seafoam smoke", "9BD9CE", roughness=0.65, emission=0.03)

    # The round visibly inflates in the bore before it is launched along +Y.
    projectile = add_sphere(
        "PROJECTILE | swelling aqua puzzle round",
        0.165,
        (0.0, 0.47, 0.925),
        aqua,
        fx_collection,
        segments=16,
        rings=10,
    )
    projectile_ring = add_torus(
        "PROJECTILE | gold equator band",
        0.112,
        0.014,
        (0.0, 0.47, 0.925),
        gold,
        fx_collection,
        axis="Y",
    )
    parent_keep_transform(projectile_ring, projectile)
    for frame, y, size in (
        (1, 0.47, 0.20),
        (3, 0.36, 0.38),
        (5, 0.15, 0.78),
        (7, 0.15, 1.28),
        (8, 0.15, 1.50),
        (9, 1.18, 1.22),
        (10, 1.80, 1.00),
        (12, 2.95, 0.86),
        (14, 4.04, 0.72),
        (15, 4.18, 0.05),
        (42, 4.18, 0.05),
    ):
        key_transform(projectile, frame, location=(0.0, y, 0.925), scale=(size, size, size))

    # Muzzle flash: fast yellow star pop and soft expanding smoke puffs.
    muzzle = add_starburst(
        "MUZZLE FX | flash starburst",
        0.31,
        (0.0, 0.80, 0.925),
        (gold, cream),
        fx_collection,
        points=8,
    )
    parent_keep_transform(muzzle, barrel)
    for frame, size in ((1, 0.001), (7, 0.001), (8, 0.18), (9, 1.0), (10, 0.88), (12, 0.24), (14, 0.001), (42, 0.001)):
        key_transform(muzzle, frame, scale=(size, size, size))

    smoke_locations = (
        (0.00, 0.93, 0.93),
        (0.00, 1.02, 1.08),
        (0.00, 1.10, 0.81),
    )
    for index, location in enumerate(smoke_locations):
        puff = add_sphere(
            f"MUZZLE FX | smoke puff {index + 1}",
            0.18,
            location,
            smoke_mat,
            fx_collection,
            segments=10,
            rings=6,
        )
        parent_keep_transform(puff, barrel)
        for frame, size in ((1, 0.001), (9, 0.001), (11, 0.40), (14, 0.78), (18, 0.95), (23, 0.001), (42, 0.001)):
            key_transform(puff, frame, scale=(size, size, size))

    # Impact boom at the end of the shot: layered star, core, shock ring and candy sparks.
    impact = (0.0, 4.18, 0.925)
    outer_burst = add_starburst(
        "IMPACT FX | coral boom burst",
        0.55,
        impact,
        (coral, gold),
        fx_collection,
        points=11,
    )
    inner_burst = add_starburst(
        "IMPACT FX | golden inner burst",
        0.37,
        (0.01, 4.20, 0.925),
        (gold, cream),
        fx_collection,
        points=9,
    )
    impact_core = add_sphere(
        "IMPACT FX | bright cream core",
        0.18,
        (0.025, 4.21, 0.925),
        cream,
        fx_collection,
        segments=12,
        rings=8,
    )
    halo = add_torus(
        "IMPACT FX | aqua shock ring",
        0.22,
        0.023,
        (0.04, 4.19, 0.925),
        aqua,
        fx_collection,
        axis="X",
    )
    for obj in (outer_burst, inner_burst, impact_core, halo):
        for frame, size in ((1, 0.001), (12, 0.001), (14, 0.18), (16, 1.0), (18, 1.15), (21, 0.82), (25, 0.001), (42, 0.001)):
            key_transform(obj, frame, scale=(size, size, size))

    shard_materials = (gold, coral, aqua, pink)
    for index in range(8):
        angle = 2.0 * math.pi * index / 8.0
        shard = add_sphere(
            f"IMPACT FX | sparkle {index + 1:02d}",
            0.075 if index % 2 == 0 else 0.055,
            impact,
            shard_materials[index % len(shard_materials)],
            fx_collection,
            segments=10,
            rings=6,
        )
        direction = Vector((0.0, math.cos(angle), math.sin(angle)))
        for frame, distance, size in ((1, 0.0, 0.001), (14, 0.0, 0.001), (16, 0.42, 1.0), (20, 0.78, 0.55), (24, 0.98, 0.001), (42, 0.98, 0.001)):
            destination = Vector(impact) + direction * distance
            key_transform(shard, frame, location=destination, scale=(size, size, size))

    # Readable timeline labels for the squash, fire, and recovery beats.
    for frame, name in (
        (1, "READY"),
        (5, "BARREL RETRACTS + SQUEEZE"),
        (8, "BARREL BULGES X/Z"),
        (9, "FIRE + RECOIL"),
        (15, "BOOM!"),
        (18, "SIDE-TO-SIDE ROCK"),
        (31, "BACK TO START"),
    ):
        scene.timeline_markers.new(name, frame=frame)

    scene.frame_set(1)
    # Keep render previews concise and compatible with Blender's bundled FFmpeg.
    scene.render.resolution_x = 960
    scene.render.resolution_y = 540
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.filepath = str(PREVIEW_PNG)
    scene.frame_set(17)
    bpy.ops.render.render(write_still=True)

    scene.frame_set(1)
    scene.render.image_settings.media_type = "VIDEO"
    scene.render.image_settings.file_format = "FFMPEG"
    scene.render.ffmpeg.format = "MPEG4"
    scene.render.ffmpeg.codec = "H264"
    scene.render.ffmpeg.constant_rate_factor = "MEDIUM"
    scene.render.ffmpeg.audio_codec = "NONE"
    scene.render.filepath = str(ANIMATION_MP4)

    # Save with the timeline at the first frame and a camera view ready to play.
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type == "VIEW_3D":
                space = area.spaces.active
                space.region_3d.view_perspective = "CAMERA"
                space.shading.type = "MATERIAL"
                if hasattr(space.overlay, "show_extras"):
                    space.overlay.show_extras = False
    bpy.ops.object.select_all(action="DESELECT")
    root.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.ops.wm.save_as_mainfile(filepath=str(ANIMATED_BLEND))

    bpy.ops.render.render(animation=True)
    print(f"Animated Blender file: {ANIMATED_BLEND}")
    print(f"Firing animation preview: {ANIMATION_MP4}")
    print(f"Boom still: {PREVIEW_PNG}")
    print("Animation: barrel retracts on Y and bulges only on X/Z, fires, then cannon recoils/rocks and returns; no Unity export or audio.")


if __name__ == "__main__":
    build_animation()
