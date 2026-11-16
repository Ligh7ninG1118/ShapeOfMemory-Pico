"""Ruin kit for Shape of Memory (MemoryClouds' ruins): weathered classical stone standing in the cloud sea at mid to far
distance (the user's reference, 2026-11-12: square pillars, arches and broken columns rising out of dusk clouds round a
floating table). Shaded in the game like the clouds (ShapeOfMemory/Cloud on Materials/Memory ruin.mat: the same light,
haze toward the sky and fade-in, a stone palette), so here they are geometry only, plus the vertex colour the shader
reads:
  R  occlusion (1 open, 0 deep under an arch or where blocks meet), from rays cast over each vertex's hemisphere
  G  height in the piece (0 base, 1 top)
Built from boxes, tapered cylinders and arch profiles extruded through the wall, with small bevels so edges catch the
light. Broken pieces are cut by one or two tilted planes and capped. Edges sharper than SHARP degrees stay hard (flat
stone faces); round shafts are smooth. Each piece's base middle is at the origin and its front (the arch's face) looks
along -Y here, +Z in Unity, so MemoryClouds can turn arches toward the player.

Run (Blender 4.2, CPU; seconds):
  blender -b --factory-startup --python Tools/ShadowArt/build_ruin_kit.py -- [--preview] [--seed N]
Writes Assets/ShadowWorkshop/PicoFinal/Art/Ruins/RuinKit.fbx; --preview also renders a contact sheet (Workbench) to
<scratch>/ruin_kit_preview.png.
"""
import bpy, bmesh, math, os, sys
import numpy as np
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree

ROOT=os.path.abspath(os.path.join(os.path.dirname(__file__),'..','..'))
OUT=os.environ.get('RUIN_OUT',os.path.join(ROOT,'Assets','ShadowWorkshop','PicoFinal','Art','Ruins'))
SCRATCH=os.environ.get('RUIN_SCRATCH',os.path.join(ROOT,'.agents','local','ruin-kit'))
args=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
def arg(name,default):
    return type(default)(args[args.index(name)+1]) if name in args else default
SEED=arg('--seed',3);PREVIEW='--preview' in args
rng=np.random.default_rng(SEED)

BEVEL=.07       # metres off each hard edge
SHARP=35        # degrees: edges sharper than this stay hard
ARC=12          # segments in a half-circle arch
AO_RAYS=24;AO_REACH=6.0  # occlusion rays per vertex and how far they look (metres)

# ---- Building blocks (into one bmesh per piece)
def box(bm,x0,x1,y0,y1,z0,z1):
    m=Matrix.Translation(((x0+x1)/2,(y0+y1)/2,(z0+z1)/2))@Matrix.Diagonal(((x1-x0),(y1-y0),(z1-z0),1))
    bmesh.ops.create_cube(bm,size=1,matrix=m)
def cylinder(bm,x,y,z0,z1,r0,r1,seg=16):
    bmesh.ops.create_cone(bm,cap_ends=True,cap_tris=False,segments=seg,radius1=r0,radius2=r1,depth=z1-z0,
        matrix=Matrix.Translation((x,y,(z0+z1)/2)))
def profile(bm,pts,y0,y1):
    """A 2D outline (x, z) extruded from y0 to y1: an arch, an arcade or a wall with a doorway."""
    front=[bm.verts.new((x,y0,z)) for x,z in pts];back=[bm.verts.new((x,y1,z)) for x,z in pts]
    bm.faces.new(front);bm.faces.new(list(reversed(back)))
    for i in range(len(pts)):
        j=(i+1)%len(pts);bm.faces.new((front[i],back[i],back[j],front[j]))
def arch_outline(width,height,openings):
    """Outline of a wall width x height (base middle at x 0) with arched openings along its foot: (middle, span,
    springing height). One simple polygon: each opening dips up into the wall from the bottom edge."""
    pts=[(-width/2,0)]
    for cx,span,spring in sorted(openings):
        pts+=[(cx-span/2,0),(cx-span/2,spring)]
        for k in range(1,ARC):
            a=math.pi-math.pi*k/ARC
            pts.append((cx+span/2*math.cos(a),spring+span/2*math.sin(a)))
        pts+=[(cx+span/2,spring),(cx+span/2,0)]
    return pts+[(width/2,0),(width/2,height),(-width/2,height)]

def bevel_hard(bm):
    bmesh.ops.recalc_face_normals(bm,faces=bm.faces[:])
    edges=[e for e in bm.edges if e.is_manifold and e.calc_face_angle(0)>math.radians(SHARP)]
    if edges:bmesh.ops.bevel(bm,geom=edges,offset=BEVEL,offset_type='OFFSET',segments=1,profile=.5,affect='EDGES',clamp_overlap=True)

def cut(bm,point,normal):
    """Break off everything beyond a plane (normal points at what goes), and cap the break."""
    res=bmesh.ops.bisect_plane(bm,geom=bm.verts[:]+bm.edges[:]+bm.faces[:],plane_co=Vector(point),plane_no=Vector(normal).normalized(),clear_outer=True)
    edges=[g for g in res['geom_cut'] if isinstance(g,bmesh.types.BMEdge)]
    if edges:bmesh.ops.holes_fill(bm,edges=edges,sides=0)
    bmesh.ops.recalc_face_normals(bm,faces=bm.faces[:])
def part(bm,make):
    """Build into a bmesh of its own (make(bm) may cut it: a cut reaches only what is in its bmesh), then add it."""
    tmp=bmesh.new();make(tmp);me=bpy.data.meshes.new('part');tmp.to_mesh(me);tmp.free();bm.from_mesh(me);bpy.data.meshes.remove(me)
def tilted(tilt,turn):
    """Up, leaned tilt degrees toward azimuth turn."""
    t,a=math.radians(tilt),math.radians(turn)
    return (math.sin(t)*math.cos(a),math.sin(t)*math.sin(a),math.cos(t))
def broken_top(bm,height,width):
    """One or two tilted cuts near the top: a slanted break or a ridge."""
    cut(bm,(0,0,height),tilted(rng.uniform(18,32),rng.uniform(0,360)))
    if rng.uniform(0,1)<.6:cut(bm,(rng.uniform(-.3,.3)*width,0,height-rng.uniform(.3,.9)),tilted(rng.uniform(35,55),rng.uniform(0,360)))

# ---- The pieces
def square_pillar(bm,h,w=2.2,capital=True):
    box(bm,-w*.68,w*.68,-w*.68,w*.68,0,.9)                       # plinth
    box(bm,-w/2,w/2,-w/2,w/2,.9,h-(1.2 if capital else 0))       # shaft
    if capital:
        box(bm,-w*.6,w*.6,-w*.6,w*.6,h-1.2,h-.55);box(bm,-w*.72,w*.72,-w*.72,w*.72,h-.55,h)
def doric(bm,x,y,h,r=1.0,capital=True):
    cylinder(bm,x,y,0,.5,r*1.35,r*1.35,20)                       # base
    cylinder(bm,x,y,.5,h-(1.1 if capital else 0),r,r*.82,16)     # shaft, tapering
    if capital:
        cylinder(bm,x,y,h-1.1,h-.5,r*.82,r*1.25,16)              # echinus
        box(bm,x-r*1.35,x+r*1.35,y-r*1.35,y+r*1.35,h-.5,h)       # abacus

def Ruin_Pillar(bm):square_pillar(bm,16)
def Ruin_PillarBroken(bm):
    square_pillar(bm,12,capital=False);bevel_hard(bm);broken_top(bm,11,2.2)
def Ruin_Column(bm):doric(bm,0,0,15)
def Ruin_ColumnBroken(bm):
    doric(bm,0,0,10,capital=False);bevel_hard(bm);broken_top(bm,8.5,2)
def Ruin_Arch(bm):
    profile(bm,arch_outline(9,14,[(0,6.2,8)]),-1.1,1.1)
    box(bm,-5.1,5.1,-1.4,1.4,14,14.8)                            # cornice
def Ruin_ArchBroken(bm):
    profile(bm,arch_outline(9,13.5,[(0,6.2,7.5)]),-1.1,1.1);bevel_hard(bm)
    cut(bm,(1.6,0,11.2),tilted(40,rng.uniform(-10,10)))         # the upper right gone
def Ruin_Arcade(bm):
    profile(bm,arch_outline(19,11,[(-6,4,5),(0,4,5),(6,4,5)]),-1,1)
    box(bm,-9.9,9.9,-1.3,1.3,11,11.7);bevel_hard(bm)
    cut(bm,(6.5,0,9),tilted(55,rng.uniform(-15,15)))            # one end broken off
def Ruin_Wall(bm):
    profile(bm,arch_outline(12,15,[(-1.5,4,7)]),-.8,.8);bevel_hard(bm)
    cut(bm,(0,0,13.5),tilted(16,180+rng.uniform(-20,20)))
    cut(bm,(4,0,11),tilted(48,rng.uniform(-15,15)))
def Ruin_Temple(bm):
    # Two whole columns under a beam that breaks off past the second; the third column broken lower down.
    for x in (-5,0):doric(bm,x,0,11,.85)
    bevel_hard(bm)
    def beam(b):box(b,-6.4,3.4,-1.3,1.3,11,12.4);bevel_hard(b);cut(b,(2.6,0,11.7),tilted(60,rng.uniform(-20,20)))
    def stump(b):doric(b,5,0,8,.85,capital=False);bevel_hard(b);cut(b,(5,0,6.6),tilted(28,rng.uniform(0,360)))
    part(bm,beam);part(bm,stump)
# A cluster of loose blocks was tried and dropped (2026-11-12): it read as a placeholder.
PIECES=[Ruin_Pillar,Ruin_PillarBroken,Ruin_Column,Ruin_ColumnBroken,Ruin_Arch,Ruin_ArchBroken,Ruin_Arcade,Ruin_Wall,Ruin_Temple]

# ---- Mesh, normals, colours
def hemisphere(n):
    dirs=[];g=math.pi*(3-math.sqrt(5))
    for i in range(n):
        u=(i+.5)/n;r=math.sqrt(u);t=g*i
        dirs.append(Vector((r*math.cos(t),r*math.sin(t),math.sqrt(max(0,1-u)))))
    return dirs

def finish(name,bm):
    if name not in ('Ruin_PillarBroken','Ruin_ColumnBroken','Ruin_ArchBroken','Ruin_Arcade','Ruin_Wall','Ruin_Temple'):bevel_hard(bm)
    bmesh.ops.remove_doubles(bm,verts=bm.verts[:],dist=1e-4)
    bmesh.ops.recalc_face_normals(bm,faces=bm.faces[:])
    # Base middle at the origin.
    xs=[v.co.x for v in bm.verts];ys=[v.co.y for v in bm.verts];zs=[v.co.z for v in bm.verts]
    shift=Vector(((min(xs)+max(xs))/2,(min(ys)+max(ys))/2,min(zs)))
    for v in bm.verts:v.co-=shift
    me=bpy.data.meshes.new(name);bm.to_mesh(me);bm.free()
    me.shade_smooth();me.set_sharp_from_angle(angle=math.radians(SHARP))
    # Occlusion and height in the vertex colour.
    verts=[v.co.copy() for v in me.vertices];polys=[tuple(p.vertices) for p in me.polygons]
    bvh=BVHTree.FromPolygons(verts,polys);dirs=hemisphere(AO_RAYS)
    occ=np.ones(len(verts),np.float32)
    for i,v in enumerate(me.vertices):
        n=v.normal;basis=n.to_track_quat('Z','Y').to_matrix();o=v.co+n*.01;hits=0
        for d in dirs:
            if bvh.ray_cast(o,basis@d,AO_REACH)[0] is not None:hits+=1
        occ[i]=(1-hits/len(dirs))**.8
    z=np.array([v.z for v in verts]);h=(z-z.min())/max(z.max()-z.min(),1e-6)
    attr=me.color_attributes.new(name='Col',type='FLOAT_COLOR',domain='POINT')
    attr.data.foreach_set('color',np.stack([occ,h,np.ones(len(z)),np.ones(len(z))],1).astype(np.float32).ravel())
    me.color_attributes.active_color=attr
    ob=bpy.data.objects.new(name,me);bpy.context.scene.collection.objects.link(ob)
    tris=sum(len(p.vertices)-2 for p in me.polygons);d=ob.dimensions
    print(f'RUIN {name}: {tris} tris, {d.x:.1f} x {d.y:.1f} x {d.z:.1f} m, open {occ.mean():.2f}')
    return ob

def export(obs):
    os.makedirs(OUT,exist_ok=True)
    bpy.ops.object.select_all(action='DESELECT')
    for o in obs:o.select_set(True)
    bpy.context.view_layer.objects.active=obs[0]
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT,'RuinKit.fbx'),use_selection=True,object_types={'MESH'},apply_unit_scale=True,
        apply_scale_options='FBX_SCALE_ALL',axis_forward='-Z',axis_up='Y',bake_space_transform=True,mesh_smooth_type='OFF',
        use_mesh_modifiers=False,use_triangles=True,colors_type='LINEAR',path_mode='STRIP',embed_textures=False,add_leaf_bones=False,bake_anim=False)

def preview(obs):
    x=0.0
    for o in obs:
        w=o.dimensions.x;o.location=(x+w*.5,0,0);x+=w+4
    sc=bpy.context.scene;sc.render.engine='BLENDER_WORKBENCH'
    sc.display.shading.light='STUDIO';sc.display.shading.color_type='OBJECT';sc.display.shading.show_cavity=True
    sc.render.resolution_x=2400;sc.render.resolution_y=520
    cam=bpy.data.objects.new('cam',bpy.data.cameras.new('cam'));sc.collection.objects.link(cam);sc.camera=cam
    cam.data.type='ORTHO';cam.data.ortho_scale=x*1.02;cam.location=(x*.5,-300,40);cam.rotation_euler=(math.radians(84),0,math.radians(0))
    for o in obs:o.color=(.82,.78,.86,1)
    os.makedirs(SCRATCH,exist_ok=True);sc.render.filepath=os.path.join(SCRATCH,'ruin_kit_preview.png')
    bpy.ops.render.render(write_still=True)
    print('RUIN preview ->',sc.render.filepath)

def main():
    for o in list(bpy.data.objects):bpy.data.objects.remove(o,do_unlink=True)
    obs=[]
    for make in PIECES:
        bm=bmesh.new();make(bm);obs.append(finish(make.__name__,bm))
    export(obs)
    print('RUIN kit ->',os.path.join(OUT,'RuinKit.fbx'),f'({len(obs)} meshes)')
    if PREVIEW:preview(obs)

main()
