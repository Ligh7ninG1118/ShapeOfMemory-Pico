"""A seated player for Shape of Memory's scene (PicoFinal): a plain mannequin on a chair, so the table, the stand, the
book and the orb can be judged against a body (the user, 2026-11-13: "something quick that would resemble a human player,
sitting up"). Editor only (MemorySeatedPlayer, on an EditorOnly object): it never ships.

An adult of STATURE (1.70 m) on a chair SEAT high, upright, forearms forward at the height of a hand reaching for the
table. Segment lengths from the usual stature ratios (Drillis & Contini): thigh .245, shank .246, upper arm .186, forearm
.146, hand .108; shoulders .245 across; seated eye height EYE over the seat. Built from metaballs (ellipsoids for the
head, chest and pelvis, capsules along the limbs) so the joints blend like an artist's mannequin, then meshed and
decimated. The front looks along -Y here, +Z in Unity, as the other kits.

Run (Blender 4.2, CPU; seconds):
  blender -b --factory-startup --python Tools/ShadowArt/build_seated_player.py -- [--preview]
Writes Assets/ShadowWorkshop/PicoFinal/Art/Dev/SeatedPlayer.fbx (meshes "Seated player" and "Chair", origin on the floor
under the eyes' midpoint... see EYE_AT below); --preview renders a side and a front view to <scratch>/seated_player.png.
"""
import bpy, math, os, sys
from mathutils import Vector, Quaternion

ROOT=os.path.abspath(os.path.join(os.path.dirname(__file__),'..','..'))
OUT=os.environ.get('SEATED_OUT',os.path.join(ROOT,'Assets','ShadowWorkshop','PicoFinal','Art','Dev'))
SCRATCH=os.environ.get('SEATED_SCRATCH',os.path.join(ROOT,'.agents','local','seated-player'))
args=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
PREVIEW='--preview' in args

STATURE=1.70
SEAT=0.45            # the chair's seat over the floor
EYE=0.765            # seated eye height over the seat (about .45 of the stature)
H=STATURE
# Joints (metres; x across, -y forward, z up from the floor).
hipZ=SEAT+.08
J=dict(
    pelvis=(0,.02,SEAT+.10), lumbar=(0,.03,SEAT+.25), chest=(0,.02,SEAT+.43), neck=(0,.01,SEAT+.60), skull=(0,-.02,SEAT+.775),
    hipL=(.09,.0,hipZ), kneeL=(.10,-.245*H,hipZ-.01), ankleL=(.10,-.245*H-.04,.085), toeL=(.10,-.245*H-.21,.03),
    shoulderL=(.122*H*.86,.0,SEAT+.575), elbowL=(.20,-.10,SEAT+.29), wristL=(.17,-.10-.146*H*.94,SEAT+.33), handL=(.155,-.10-.146*H*.94-.108*H*.8,SEAT+.335),
)
for k in list(J):
    if k.endswith('L'):J[k[:-1]+'R']=(-J[k][0],J[k][1],J[k][2])
EYE_AT=Vector((0,-.02-.085,SEAT+EYE))   # the eyes' midpoint: in front of the skull's middle

def clear():
    for o in list(bpy.data.objects):bpy.data.objects.remove(o,do_unlink=True)
    for m in list(bpy.data.metaballs):bpy.data.metaballs.remove(m)
    for m in list(bpy.data.meshes):bpy.data.meshes.remove(m)

def mannequin():
    # Exact-size parts (ellipsoids, tapered capsules), fused by a voxel remesh and smoothed: radii are real widths.
    import bmesh
    bm=bmesh.new()
    def ellipsoid(at,r):
        tmp=bmesh.new();bmesh.ops.create_uvsphere(tmp,u_segments=32,v_segments=16,radius=1)
        for v in tmp.verts:v.co=Vector((at[0]+v.co.x*r[0],at[1]+v.co.y*r[1],at[2]+v.co.z*r[2]))
        add(tmp)
    def capsule(a,b,ra,rb):
        a=Vector(a);b=Vector(b);d=b-a;n=24
        tmp=bmesh.new();bmesh.ops.create_cone(tmp,cap_ends=True,segments=n,radius1=ra,radius2=rb,depth=d.length)
        q=Vector((0,0,1)).rotation_difference(d.normalized())
        for v in tmp.verts:v.co=q@v.co+(a+b)*.5
        add(tmp);ellipsoid(a,(ra,ra,ra));ellipsoid(b,(rb,rb,rb))
    def add(tmp):
        me=bpy.data.meshes.new('tmp');tmp.to_mesh(me);tmp.free();bm.from_mesh(me);bpy.data.meshes.remove(me)
    eyes=EYE_AT
    # Head: skull, jaw; neck.
    head=Vector((0,-.02,eyes.z+.01))
    ellipsoid(head,(.076,.096,.116))
    ellipsoid((0,-.052,eyes.z-.068),(.058,.062,.05))
    capsule((0,.0,SEAT+.57),(0,-.012,eyes.z-.05),.056,.05)
    # Trunk: ribcage, belly, pelvis; the shoulders' round.
    ellipsoid((0,.015,SEAT+.44),(.158,.108,.17))
    ellipsoid((0,.022,SEAT+.28),(.138,.098,.12))
    ellipsoid((0,.03,SEAT+.11),(.168,.118,.10))
    for s in(1,-1):
        sh=Vector((s*.172,.01,SEAT+.575));el=Vector((s*.198,-.08,SEAT+.29));wr=Vector((s*.168,-.31,SEAT+.33))
        ellipsoid(sh,(.056,.058,.06))
        capsule(sh,el,.045,.037)
        capsule(el,wr,.037,.027)
        ellipsoid((s*.158,-.39,SEAT+.335),(.042,.088,.019))      # the hand, flat, fingers forward
        hip=Vector((s*.09,.02,SEAT+.08));kn=Vector((s*.10,-.40,SEAT+.07));an=Vector((s*.10,-.44,.085));toe=Vector((s*.10,-.615,.035))
        capsule(hip,kn,.082,.055)
        capsule(kn,an,.05,.034)
        capsule((an.x,an.y+.03,.06),toe,.042,.03)
    me=bpy.data.meshes.new('Seated player');bm.to_mesh(me);bm.free()
    ob=bpy.data.objects.new('Seated player',me);bpy.context.scene.collection.objects.link(ob)
    return ob

def to_mesh(ob,target_tris=7000):
    bpy.context.view_layer.objects.active=ob;ob.select_set(True)
    ob.data.remesh_voxel_size=.008;ob.data.remesh_voxel_adaptivity=0
    bpy.ops.object.voxel_remesh()
    sm=ob.modifiers.new('Smooth','SMOOTH');sm.factor=.6;sm.iterations=6
    bpy.ops.object.modifier_apply(modifier=sm.name)
    tris=sum(len(p.vertices)-2 for p in ob.data.polygons)
    if tris>target_tris:
        d=ob.modifiers.new('Decimate','DECIMATE');d.ratio=target_tris/tris
        bpy.ops.object.modifier_apply(modifier=d.name)
    for p in ob.data.polygons:p.use_smooth=True
    return ob

def box(name,lo,hi):
    import bmesh
    bm=bmesh.new();bmesh.ops.create_cube(bm,size=1)
    for v in bm.verts:v.co=Vector([(lo[i]+hi[i])*.5+v.co[i]*(hi[i]-lo[i]) for i in range(3)])
    me=bpy.data.meshes.new(name);bm.to_mesh(me);bm.free();return me

def chair():
    import bmesh
    parts=[]
    w,d,t=.46,.44,.035;cy=.05   # seat: under the thighs and behind the hips
    parts.append(((-w/2,cy-d/2,SEAT-t),(w/2,cy+d/2,SEAT)))
    for sx in(-1,1):
        for sy in(-1,1):
            x=sx*(w/2-.03);y=cy+sy*(d/2-.03);parts.append(((x-.018,y-.018,0),(x+.018,y+.018,SEAT-t)))
    # back: two posts and a rail
    for sx in(-1,1):
        x=sx*(w/2-.03);y=cy+d/2-.02;parts.append(((x-.018,y-.015,SEAT),(x+.018,y+.015,SEAT+.42)))
    parts.append(((-w/2+.01,cy+d/2-.035,SEAT+.26),(w/2-.01,cy+d/2-.005,SEAT+.42)))
    bm=bmesh.new()
    for lo,hi in parts:
        tmp=bmesh.new();bmesh.ops.create_cube(tmp,size=1)
        for v in tmp.verts:v.co=Vector([(lo[i]+hi[i])*.5+v.co[i]*(hi[i]-lo[i]) for i in range(3)])
        me=bpy.data.meshes.new('tmp');tmp.to_mesh(me);tmp.free();bm.from_mesh(me);bpy.data.meshes.remove(me)
    me=bpy.data.meshes.new('Chair');bm.to_mesh(me);bm.free()
    ob=bpy.data.objects.new('Chair',me);bpy.context.scene.collection.objects.link(ob);return ob

def preview(obs):
    sc=bpy.context.scene;sc.render.engine='BLENDER_WORKBENCH'
    sc.display.shading.light='STUDIO';sc.display.shading.color_type='OBJECT';sc.display.shading.show_cavity=True
    sc.render.resolution_x=1400;sc.render.resolution_y=800
    obs[0].color=(.78,.80,.86,1);obs[1].color=(.45,.33,.25,1)
    # floor and an eye marker
    fl=bpy.data.objects.new('Floor',box('Floor',(-1,-1.2,-.01),(1,.8,0)));sc.collection.objects.link(fl);fl.color=(.3,.3,.32,1)
    eye=bpy.data.objects.new('Eye',box('Eye',EYE_AT-Vector((.01,.01,.01)),EYE_AT+Vector((.01,.01,.01))));sc.collection.objects.link(eye);eye.color=(1,.2,.2,1)
    cam=bpy.data.objects.new('cam',bpy.data.cameras.new('cam'));sc.collection.objects.link(cam);sc.camera=cam
    cam.data.type='ORTHO';cam.data.ortho_scale=2.2
    os.makedirs(SCRATCH,exist_ok=True)
    for label,loc,rot in(('side',(6,-.2,.75),(math.radians(90),0,math.radians(90))),('front',(0,-6,.75),(math.radians(90),0,0)),('three',(3.2,-3.8,2.0),(math.radians(68),0,math.radians(40)))):
        cam.location=loc;cam.rotation_euler=rot
        sc.render.filepath=os.path.join(SCRATCH,'seated_player_%s.png'%label);bpy.ops.render.render(write_still=True)
        print('SEATED preview ->',sc.render.filepath)
    bpy.data.objects.remove(fl,do_unlink=True);bpy.data.objects.remove(eye,do_unlink=True);bpy.data.objects.remove(cam,do_unlink=True)

def main():
    clear()
    person=to_mesh(mannequin());person.name='Seated player';person.data.name='Seated player'
    seat=chair()
    obs=[person,seat]
    tris=sum(len(p.vertices)-2 for p in person.data.polygons)
    print(f'SEATED person {tris} tris, {person.dimensions.x:.2f} x {person.dimensions.y:.2f} x {person.dimensions.z:.2f} m; eyes at {tuple(round(c,3) for c in EYE_AT)}')
    if PREVIEW:preview(obs)
    os.makedirs(OUT,exist_ok=True)
    bpy.ops.object.select_all(action='DESELECT')
    for o in obs:o.select_set(True)
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT,'SeatedPlayer.fbx'),use_selection=True,object_types={'MESH'},apply_unit_scale=True,
        apply_scale_options='FBX_SCALE_ALL',axis_forward='-Z',axis_up='Y',bake_space_transform=True,mesh_smooth_type='FACE',
        use_mesh_modifiers=True,use_triangles=True,path_mode='STRIP',embed_textures=False,add_leaf_bones=False,bake_anim=False)
    print('SEATED ->',os.path.join(OUT,'SeatedPlayer.fbx'))

main()
