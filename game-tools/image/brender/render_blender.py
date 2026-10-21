"""blender -b -P render_blender.py -- prims.json outdir engine basezoom -> <frame>_hi.png (3x), downscale with sheet.py"""
import bpy, bmesh, json, math, sys, os, time
from mathutils import Matrix, Vector
a = sys.argv[sys.argv.index("--") + 1:]
data = json.load(open(a[0])); outdir = a[1]; engine = a[2] if len(a) > 2 else "BLENDER_EEVEE_NEXT"
os.makedirs(outdir, exist_ok=True)
W, H, SSX = 160, 200, 3
BASE_ZOOM = float(a[3]) if len(a) > 3 else 1.0   # px per model unit at 64 wide; passed in
ZOOM = BASE_ZOOM * W / 64
AX, AY = W // 2, H * 9 // 10
CONV = Matrix(((1, 0, 0, 0), (0, 0, -1, 0), (0, 1, 0, 0), (0, 0, 0, 1)))   # model(y up,z to viewer) -> blender(z up, viewer at -y)
def lin(c): return (c / 255.0) ** 2.2
bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene
sc.render.engine = engine
sc.render.resolution_x, sc.render.resolution_y = W * SSX, H * SSX
sc.render.film_transparent = True
sc.view_settings.view_transform = "Standard"
if engine == "BLENDER_EEVEE_NEXT": sc.eevee.taa_render_samples = 24
# world: soft ambient
wd = bpy.data.worlds.new("w"); sc.world = wd; wd.use_nodes = True
bg = wd.node_tree.nodes["Background"]; bg.inputs[0].default_value = (0.75, 0.8, 0.9, 1); bg.inputs[1].default_value = 0.55
# lights (model LIGHT=(-.5,.8,.45): from upper-left, toward viewer)
def sun(name, direction_from, energy, col=(1, 0.96, 0.9)):
    l = bpy.data.lights.new(name, "SUN"); l.energy = energy; l.color = col; l.angle = math.radians(25)
    o = bpy.data.objects.new(name, l); sc.collection.objects.link(o)
    v = Vector(direction_from); o.rotation_euler = (-v).to_track_quat("-Z", "Y").to_euler()
sun("key", (-0.5, -0.45, 0.8), 3.2)       # blender coords: from left, viewer side, above
sun("fill", (0.7, -0.4, 0.3), 0.8, (0.8, 0.88, 1))
cam_d = bpy.data.cameras.new("c"); cam_d.type = "ORTHO"; cam_d.ortho_scale = W / ZOOM
cam = bpy.data.objects.new("c", cam_d); sc.collection.objects.link(cam); sc.camera = cam
E = math.radians(36)
view = Vector((0, -math.cos(E), math.sin(E)))
cam.rotation_euler = (math.radians(90) - E, 0, 0)
upv = Vector((0, math.sin(E), math.cos(E)))

def mk_mat(col, team_mask=None):
    m = bpy.data.materials.new("m"); m.use_nodes = True; nt = m.node_tree
    if team_mask is not None:
        for n in list(nt.nodes): nt.nodes.remove(n)
        e = nt.nodes.new("ShaderNodeEmission"); v = 1.0 if team_mask else 0.0
        e.inputs[0].default_value = (v, v, v, 1); o = nt.nodes.new("ShaderNodeOutputMaterial"); nt.links.new(e.outputs[0], o.inputs[0]); return m
    b = nt.nodes["Principled BSDF"]; b.inputs["Base Color"].default_value = (lin(col[0]), lin(col[1]), lin(col[2]), 1)
    b.inputs["Roughness"].default_value = 0.65; b.inputs["Specular IOR Level"].default_value = 0.25
    return m
mats = {}
def get_mat(col, team, mask):
    key = (tuple(col), team, mask)
    if key not in mats: mats[key] = mk_mat(col, team if mask else None)
    return mats[key]

def prim_bm(bm, p):
    M = CONV @ Matrix([p["M"][i*4:(i+1)*4] for i in range(4)])
    d = p["d"]; k = p["k"]
    Y2Z = Matrix.Rotation(math.radians(-90), 4, "X")   # blender cylinder along z -> local-y axis conversion handled via CONV: local y -> b.-z? use explicit
    # local prim frame is model space; CONV already maps it, so build geometry in model-local coords then transform by M
    def add(fn, mat):
        r = fn(); return r
    mm = M
    if k == "box" or k == "rbox":
        sx, sy, sz = d[0], d[1], d[2]
        bmesh.ops.create_cube(bm, size=2.0, matrix=mm @ Matrix.Diagonal((sx, sy, sz, 1)))
    elif k == "ell":
        bmesh.ops.create_uvsphere(bm, u_segments=20, v_segments=12, radius=1.0, matrix=mm @ Matrix.Diagonal((d[0], d[1], d[2], 1)) @ Matrix.Rotation(math.radians(90), 4, "X"))
    elif k in ("cap", "cyl", "cone"):
        r, hl = (d[0], d[1]) if k != "cone" else (d[1], d[0])
        r1 = r if k != "cone" else d[2]
        A = mm @ Matrix.Rotation(math.radians(-90), 4, "X")    # local z(cylinder axis) -> local +y? rotate X(-90): z->y... verified below
        # create_cone along +z, base radius1 at -z; X rot of -90 maps z -> +y? (y' = z*... ) use +90 if reversed
        bmesh.ops.create_cone(bm, cap_ends=True, segments=16, radius1=r, radius2=r1, depth=2 * hl, matrix=A)
        if k == "cap":
            for s in (-1, 1):
                bmesh.ops.create_uvsphere(bm, u_segments=16, v_segments=8, radius=r, matrix=mm @ Matrix.Translation((0, s * hl, 0)))
    return

def build(frame, mask):
    ob_list = []
    groups = {}
    for p in frame["prims"]:
        key = (tuple(p["c"]), p["team"], p["k"] == "rbox", tuple(p["d"][3:4]))
        groups.setdefault(key, []).append(p)
    for (col, team, rb, rr), ps in groups.items():
        bm = bmesh.new()
        for p in ps: prim_bm(bm, p)
        me = bpy.data.meshes.new("g"); bm.to_mesh(me); bm.free()
        for f in me.polygons: f.use_smooth = True
        ob = bpy.data.objects.new("g", me); sc.collection.objects.link(ob)
        ob.data.materials.append(get_mat(col, team, mask))
        if rb:
            bv = ob.modifiers.new("b", "BEVEL"); bv.width = 0.8; bv.segments = 3; bv.limit_method = "ANGLE"
        sm = ob.modifiers.new("s", "SUBSURF"); sm.levels = 1; sm.render_levels = 1
        ob_list.append(ob)
    return ob_list

for name, fr in data["frames"].items():
    t0 = time.time()
    lift = fr["lift"] * W / 64
    off = (AY - lift) - H / 2     # px the ground origin sits below image centre
    tgt = Vector((0, 0, 0)) + Vector((0, upv.y, upv.z)) * (off / ZOOM)
    cam.location = tgt + view * 100
    for mask in (False, True):
        obs = build(fr, mask)
        sc.render.filepath = os.path.join(outdir, f"{name}{'_team' if mask else ''}_hi.png")
        if mask:
            sc.view_settings.view_transform = "Standard"; wd.node_tree.nodes["Background"].inputs[1].default_value = 0
        bpy.ops.render.render(write_still=True)
        for o in obs: bpy.data.objects.remove(o)
        if mask: wd.node_tree.nodes["Background"].inputs[1].default_value = 0.55
    print(name, "%.1fs" % (time.time() - t0), flush=True)
