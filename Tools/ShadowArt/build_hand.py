"""Shape of Memory's hand (PicoFinal): one stylised right hand, rigged to the 26 OpenXR hand joints, for MemoryHandSkin
(2026-11-14, the user: "go with owning blender mesh"; the looks are Ink and Lantern). Ours outright, so it ships on any
headset and can be restyled freely (the SDKs' hand meshes can't: see the memory note on hand meshes).

The hand is built round its joints in a relaxed rest pose. Since 2026-11-15 (the user: the capsule hand looked "chubby
and generated with primitive shapes"; make it realistic like the SDK sample hands, from our own numbers) it is one smooth
signed-distance shape (hand_sdf.py: swept elliptical fingers tapering to the tips, knuckles, a rounded palm with its pads,
webs, a wrist), meshed by surface nets at 0.8 mm and remeshed into even 3 mm quads. --capsules builds the 2026-11-14
hand instead (a tapered capsule per finger segment with a ball at each joint, a palm hull, pads; voxel fused, smoothed,
decimated). Then an armature with one bone per joint segment weights it automatically (bone heat), so a curling finger
only pulls its own skin.

Coordinates are Unity's, for a RIGHT hand, numbers as they are (nothing is converted on the way): metres, the wrist
joint at the origin, fingers along +Y, the palm facing -Z, the thumb toward +X. Blender sees the same numbers as a left
hand; nothing here depends on that. Triangles keep outward cross products, which Unity also takes as front faces; the
importer flips only the left hand (its mirror).

Bones are the OpenXR joints in OpenXR order (Oculus.Interaction.Input.HandJointId: Palm 0, Wrist 1, Thumb 2-5,
Index 6-10, Middle 11-15, Ring 16-20, Little 21-25). Each joint's bone owns the segment to its child; the palm and the
tips own no skin.

Run (Blender 4.2, CPU; about a minute):
  blender -b --factory-startup --python Tools/ShadowArt/build_hand.py -- [--preview] [--capsules]
Writes Assets/ShadowWorkshop/PicoFinal/Art/Hands/Source/MemoryHand.json (joints, vertices, normals, triangles, four bone
weights a vertex); MetaHackthone > Pico Final > Hands > Import hand mesh turns it into MemoryHand_R.asset and
MemoryHand_L.asset. --preview renders the hand to .agents/local/hands/.
"""
import bpy, bmesh, json, math, os, sys
from mathutils import Vector

ROOT=os.path.abspath(os.path.join(os.path.dirname(__file__),'..','..'))
OUT=os.environ.get('HAND_OUT',os.path.join(ROOT,'Assets','ShadowWorkshop','PicoFinal','Art','Hands','Source'))
SCRATCH=os.environ.get('HAND_SCRATCH',os.path.join(ROOT,'.agents','local','hands'))
args=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
PREVIEW='--preview' in args

VOXEL=.0013          # remesh size: fine enough to keep the gaps between the fingers
TARGET_TRIS=7600     # after decimation (the realistic hand keeps more of its shape; the capsule hand had 5200)
REMESH=.0034         # the realistic hand's even quads (about 11k triangles; the Ink look draws a hand 4 times)

FINGERS=['Index','Middle','Ring','Little']
JOINTS=['Palm','Wrist','ThumbMetacarpal','ThumbProximal','ThumbDistal','ThumbTip']
for f in FINGERS:JOINTS+=[f+'Metacarpal',f+'Proximal',f+'Intermediate',f+'Distal',f+'Tip']
assert len(JOINTS)==26

PALM_NORMAL=Vector((0,0,-1))
# Per finger: metacarpal base B, knuckle K (the proximal joint), direction in the palm plane, segment lengths
# (proximal, intermediate, distal to the tip) and radii at the knuckle, the two finger joints and the tip.
FING=dict(
    Index=dict(B=(.013,.022,.004),K=(.026,.088,0),f=(.13,1,0),L=(.040,.023,.021),R=(.0102,.0092,.0082,.0076)),
    Middle=dict(B=(.003,.024,.005),K=(.005,.092,.002),f=(.01,1,0),L=(.044,.027,.022),R=(.0106,.0096,.0085,.0078)),
    Ring=dict(B=(-.007,.022,.004),K=(-.015,.087,0),f=(-.11,1,0),L=(.041,.026,.021),R=(.0099,.0090,.0080,.0074)),
    Little=dict(B=(-.016,.019,.002),K=(-.032,.076,-.004),f=(-.24,1,0),L=(.033,.019,.019),R=(.0086,.0078,.0070,.0065)),
)
FLEX=(6,9,6)   # the rest pose's curl at the knuckle, middle and last joints (degrees), toward the palm
# The thumb: metacarpal base, direction, the plane it bends in, segment lengths, radii, rest bends at its joints.
THUMB=dict(B=(.019,.026,-.009),d=(.62,.74,-.25),bend=(-.60,.30,-.74),L=(.043,.032,.027),R=(.0128,.0110,.0098,.0086),flex=(0,12,11))

def rest_joints():
    J={'Wrist':Vector((0,0,0))}
    for name,F in FING.items():
        B,K,f=Vector(F['B']),Vector(F['K']),Vector(F['f']).normalized()
        J[name+'Metacarpal']=B;J[name+'Proximal']=K
        p=K.copy();phi=0
        for s,label in enumerate(('Intermediate','Distal','Tip')):
            phi+=math.radians(FLEX[s])
            d=f*math.cos(phi)+PALM_NORMAL*math.sin(phi)
            p=p+d*F['L'][s];J[name+label]=p.copy()
    T=THUMB;d=Vector(T['d']).normalized();bend=Vector(T['bend']).normalized()
    p=Vector(T['B']);J['ThumbMetacarpal']=p.copy()
    for s,label in enumerate(('Proximal','Distal','Tip')):
        if s>0:
            bp=(bend-d*bend.dot(d)).normalized();a=math.radians(T['flex'][s]*(1.15 if s==2 else 1))
            d=(d*math.cos(a)+bp*math.sin(a)).normalized()
        p=p+d*T['L'][s];J['Thumb'+label]=p.copy()
    J['Palm']=(J['Wrist']+J['MiddleProximal'])*.5
    return J

def clear():
    for o in list(bpy.data.objects):bpy.data.objects.remove(o,do_unlink=True)
    for m in list(bpy.data.meshes):bpy.data.meshes.remove(m)
    for a in list(bpy.data.armatures):bpy.data.armatures.remove(a)

class Parts:
    def __init__(self):self.bm=bmesh.new()
    def add(self,tmp):
        me=bpy.data.meshes.new('tmp');tmp.to_mesh(me);tmp.free();self.bm.from_mesh(me);bpy.data.meshes.remove(me)
    def ellipsoid(self,at,r):
        tmp=bmesh.new();bmesh.ops.create_uvsphere(tmp,u_segments=28,v_segments=14,radius=1)
        for v in tmp.verts:v.co=Vector((at[0]+v.co.x*r[0],at[1]+v.co.y*r[1],at[2]+v.co.z*r[2]))
        self.add(tmp)
    def ball(self,at,r):self.ellipsoid(at,(r,r,r))
    def capsule(self,a,b,ra,rb,flat=(1.06,.92),ends=False):
        # A tapered tube, a little wider across the hand than through it (flat: across, through); ends: round caps of
        # the same flattened section (the joints have their own balls).
        a=Vector(a);b=Vector(b);d=b-a;n=24
        tmp=bmesh.new();bmesh.ops.create_cone(tmp,cap_ends=True,segments=n,radius1=1,radius2=1,depth=1)
        z=d.normalized();x=Vector((1,0,0));x=(x-z*x.dot(z)).normalized();y=z.cross(x)
        for v in tmp.verts:
            t=v.co.z+.5;r=ra+(rb-ra)*t
            v.co=a+d*t+x*(v.co.x*r*flat[0])+y*(v.co.y*r*flat[1])
        self.add(tmp)
        if ends:
            for c,r in((a,ra),(b,rb)):
                tmp=bmesh.new();bmesh.ops.create_uvsphere(tmp,u_segments=24,v_segments=12,radius=1)
                for v in tmp.verts:v.co=c+x*(v.co.x*r*flat[0])+y*(v.co.y*r*flat[1])+z*(v.co.z*r*.8)
                self.add(tmp)
    def hull_of_balls(self,balls):
        # The convex hull round a few balls: a smooth wedge (a muscle pad running into the palm).
        pts=[]
        for c,r in balls:
            c=Vector(c)
            for i in range(6):
                el=math.pi*(i+.5)/6-math.pi/2
                for k in range(10):
                    az=2*math.pi*k/10;pts.append(c+Vector((math.cos(el)*math.cos(az),math.cos(el)*math.sin(az),math.sin(el)))*r)
        self.hull(pts)
    def hull(self,points):
        tmp=bmesh.new()
        for p in points:tmp.verts.new(p)
        bmesh.ops.convex_hull(tmp,input=tmp.verts)
        self.add(tmp)
    def object(self,name):
        me=bpy.data.meshes.new(name);self.bm.to_mesh(me);self.bm.free()
        ob=bpy.data.objects.new(name,me);bpy.context.scene.collection.objects.link(ob);return ob

def build_parts(J):
    P=Parts()
    for name,F in FING.items():
        R=F['R'];K,I,D,T=(J[name+s] for s in('Proximal','Intermediate','Distal','Tip'))
        cap=T-(T-D).normalized()*R[3]
        P.ball(K,R[0]*1.06)
        P.capsule(K,I,R[0],R[1]);P.ball(I,R[1]*1.02)
        P.capsule(I,D,R[1],R[2]);P.ball(D,R[2]*1.02)
        P.capsule(D,cap,R[2],R[3]);P.ball(cap,R[3])
        # The metacarpal under the back of the hand, so the knuckles' ridges run softly into the palm.
        P.capsule(J[name+'Metacarpal']+Vector((0,0,-.002)),K+Vector((0,-.004,-.0015)),R[0]*.62,R[0]*.72)
    # The webs between neighbouring fingers, just past the knuckles on the palm side, so the gaps close softly.
    for a,b in(('Index','Middle'),('Middle','Ring'),('Ring','Little')):
        ka,kb=J[a+'Proximal'],J[b+'Proximal'];ia,ib=J[a+'Intermediate'],J[b+'Intermediate']
        w=(ka.lerp(ia,.12)+kb.lerp(ib,.12))*.5+Vector((0,-.002,.0005))
        P.ellipsoid(w,(.0068,.0072,.0050))
    R=THUMB['R'];M,Pr,D,T=(J['Thumb'+s] for s in('Metacarpal','Proximal','Distal','Tip'))
    cap=T-(T-D).normalized()*R[3]
    P.capsule(M,Pr,R[0],R[1],flat=(1.05,.95));P.ball(Pr,R[1]*1.03)
    P.capsule(Pr,D,R[1],R[2]);P.ball(D,R[2]*1.02)
    P.capsule(D,cap,R[2],R[3]);P.ball(cap,R[3])
    # Pads: the thumb's (thenar) wraps its metacarpal and runs into the palm; the little finger's side (hypothenar).
    P.hull_of_balls([(M,.0125),(M.lerp(Pr,.6),.0118),((.004,.022,-.006),.010),((.006,.050,-.007),.009),((.012,.034,-.008),.011)])
    P.hull_of_balls([((-.026,.026,-.004),.0105),((-.030,.052,-.004),.0098),((-.014,.040,-.006),.009)])
    # The web between the thumb and the index.
    P.capsule(M.lerp(Pr,.72),J['IndexProximal']+Vector((-.004,-.016,-.003)),.0068,.0062,ends=True)
    # Palm: a hull through its outline at several depths, rounded at the back and palm edges, thinner toward the
    # knuckles.
    outline=[(.020,.004),(.024,.030),(.030,.076),(.026,.084),(.005,.088),(-.015,.083),(-.031,.072),(-.037,.061),(-.034,.034),(-.021,.006)]
    # Catmull-Rom through the outline, so the hull has many small faces round its rim instead of a few flat ones.
    dense=[]
    for i in range(len(outline)):
        p0,p1,p2,p3=(Vector(outline[(i+k)%len(outline)]) for k in(-1,0,1,2))
        for s in range(4):
            t=s/4;dense.append(.5*((2*p1)+(-p0+p2)*t+(2*p0-5*p1+4*p2-p3)*t*t+(-p0+3*p1-3*p2+p3)*t*t*t))
    pts=[]
    for x,y in dense:
        tf=1-.22*min(1,max(0,y/.09))
        for dz,s in((.0098,.78),(.0082,.93),(.0045,1.0),(-.0055,1.0),(-.0118,.95),(-.0150,.82)):
            pts.append(Vector((x*s,y*s+.045*(1-s),dz*tf)))
    P.hull(pts)
    # Wrist: a flattened stub reaching back 4.6 cm, wider across than through, narrowing into the heel of the hand;
    # round at its far end (the importer fades it out there).
    P.capsule((0,-.046,.0005),(0,.008,.0005),.0212,.0186,flat=(1.16,.74),ends=True)
    return P.object('Memory hand')

def to_mesh(ob):
    bpy.context.view_layer.objects.active=ob;ob.select_set(True)
    ob.data.remesh_voxel_size=VOXEL;ob.data.remesh_voxel_adaptivity=0
    bpy.ops.object.voxel_remesh()
    # Volume-keeping smoothing: rounds the parts' seams and the hull's planes without thinning the fingers.
    sm=ob.modifiers.new('Smooth','LAPLACIANSMOOTH');sm.lambda_factor=.9;sm.iterations=14;sm.use_volume_preserve=True;sm.use_normalized=True
    bpy.ops.object.modifier_apply(modifier=sm.name)
    sm=ob.modifiers.new('Smooth2','SMOOTH');sm.factor=.4;sm.iterations=3
    bpy.ops.object.modifier_apply(modifier=sm.name)
    tris=sum(len(p.vertices)-2 for p in ob.data.polygons)
    if tris>TARGET_TRIS:
        d=ob.modifiers.new('Decimate','DECIMATE');d.ratio=TARGET_TRIS/tris
        bpy.ops.object.modifier_apply(modifier=d.name)
    for p in ob.data.polygons:p.use_smooth=True
    return ob

# Each joint's bone runs to its child; deforming bones are the ones that own skin.
def child_of(name):
    if name=='Palm':return None
    if name=='Wrist':return None
    for f in ['Thumb']+FINGERS:
        if name.startswith(f):
            chain=[f+s for s in(('Metacarpal','Proximal','Distal','Tip') if f=='Thumb' else('Metacarpal','Proximal','Intermediate','Distal','Tip'))]
            i=chain.index(name);return chain[i+1] if i+1<len(chain) else None
    return None

def rig(ob,J):
    arm=bpy.data.armatures.new('Memory hand rig');ao=bpy.data.objects.new('Memory hand rig',arm)
    bpy.context.scene.collection.objects.link(ao)
    bpy.context.view_layer.objects.active=ao;bpy.ops.object.mode_set(mode='EDIT')
    for name in JOINTS:
        b=arm.edit_bones.new(name);c=child_of(name)
        if name=='Wrist':b.head=Vector((0,-.044,.001));b.tail=Vector((0,.012,.001))
        elif c:b.head=J[name];b.tail=J[c]
        elif name.endswith('Tip'):
            b.head=J[name];b.tail=J[name]+(J[name]-J[name[:-3]+'Distal']).normalized()*.004
        else:b.head=J[name];b.tail=J[name]+Vector((0,.006,0))
        b.use_deform=name not in('Palm',) and not name.endswith('Tip')
    bpy.ops.object.mode_set(mode='OBJECT')
    bpy.ops.object.select_all(action='DESELECT')
    ob.select_set(True);ao.select_set(True);bpy.context.view_layer.objects.active=ao
    bpy.ops.object.parent_set(type='ARMATURE_AUTO')
    return ao

PALM_BONES={JOINTS.index(f+'Metacarpal') for f in FINGERS}

def export(ob,J):
    me=ob.data;me.calc_loop_triangles()
    index={n:i for i,n in enumerate(JOINTS)}
    groups={g.index:index[g.name] for g in ob.vertex_groups if g.name in index}
    verts=[];norms=[];weights=[];unweighted=0
    for v in me.vertices:
        verts+= [round(c,6) for c in v.co];norms+=[round(c,5) for c in v.normal]
        raw={}
        for g in v.groups:
            if g.group in groups and g.weight>1e-4:
                bone=groups[g.group]
                # The palm is one rigid block on the wrist's frame (2026-11-15): the finger metacarpals' bones are
                # wherever the runtime puts those joints (and they move as a hand cups), and skin bound to them folded
                # the back of the hand. The knuckles still blend into the fingers' own bones.
                if bone in PALM_BONES:bone=index['Wrist']
                raw[bone]=raw.get(bone,0)+g.weight
        ws=sorted(((w,b) for b,w in raw.items()),reverse=True)[:4]
        total=sum(w for w,_ in ws)
        if total<=0:unweighted+=1;ws=[(1.0,index['Wrist'])];total=1
        ws=[(w/total,b) for w,b in ws]+[(0.0,0)]*(4-len(ws))
        for w,b in ws:weights+=[b,round(w,5)]
    tris=[]
    for t in me.loop_triangles:tris+=list(t.vertices)
    # Flat arrays throughout (Unity's JsonUtility reads no nested ones): rest is x,y,z per joint in JOINTS order.
    data=dict(joints=JOINTS,rest=[round(c,6) for n in JOINTS for c in J[n]],vertices=verts,normals=norms,triangles=tris,weights=weights)
    os.makedirs(OUT,exist_ok=True);path=os.path.join(OUT,'MemoryHand.json')
    with open(path,'w') as fh:json.dump(data,fh,separators=(',',':'))
    print(f'HAND -> {path}: {len(me.vertices)} vertices, {len(me.loop_triangles)} triangles, {unweighted} unweighted (bound to the wrist)')

def preview(ob,ao):
    sc=bpy.context.scene;sc.render.engine='BLENDER_WORKBENCH'
    sc.display.shading.light='STUDIO';sc.display.shading.color_type='OBJECT';sc.display.shading.show_cavity=True
    sc.render.resolution_x=1200;sc.render.resolution_y=900
    ob.color=(.86,.80,.72,1);ao.hide_render=True
    cam=bpy.data.objects.new('cam',bpy.data.cameras.new('cam'));sc.collection.objects.link(cam);sc.camera=cam
    cam.data.type='ORTHO';cam.data.ortho_scale=.26
    os.makedirs(SCRATCH,exist_ok=True)
    c=Vector((0,.075,0))
    for label,off in(('back',(0,0,1)),('palm',(0,0,-1)),('side',(1,0,0)),('three',(.6,-.35,.75))):
        d=Vector(off).normalized();cam.location=c+d*.5
        cam.rotation_euler=(-d).to_track_quat('-Z','Y').to_euler()
        sc.render.filepath=os.path.join(SCRATCH,'hand_%s.png'%label);bpy.ops.render.render(write_still=True)
    print('HAND previews ->',SCRATCH)

def build_sdf(J):
    """The realistic hand (default since 2026-11-15): hand_sdf.py's smooth distance field, meshed by surface nets at
    0.8 mm, smoothed a little and decimated to TARGET_TRIS. --capsules builds the 2026-11-14 capsule hand instead."""
    sys.path.insert(0,os.path.dirname(os.path.abspath(__file__)))
    import hand_sdf, time
    t0=time.time()
    F=hand_sdf.build({k:tuple(v) for k,v in J.items()})
    verts,faces=hand_sdf.surface_nets(F)
    print('HAND field',tuple(F.n),'in %.1f s:'%(time.time()-t0),len(verts),'vertices,',len(faces),'quads')
    me=bpy.data.meshes.new('Memory hand');me.from_pydata(verts.tolist(),[],faces.tolist());me.validate();me.update()
    ob=bpy.data.objects.new('Memory hand',me);bpy.context.scene.collection.objects.link(ob)
    bm=bmesh.new();bm.from_mesh(me)
    vol=bm.calc_volume(signed=True)
    if vol<0:
        bmesh.ops.reverse_faces(bm,faces=bm.faces)
    # one closed piece: drop any stray islands the grid's edge might leave
    bm.verts.ensure_lookup_table()
    seen=set();islands=[]
    for v in bm.verts:
        if v.index in seen:continue
        stack=[v];comp=[]
        seen.add(v.index)
        while stack:
            u=stack.pop();comp.append(u)
            for e in u.link_edges:
                w=e.other_vert(u)
                if w.index not in seen:seen.add(w.index);stack.append(w)
        islands.append(comp)
    islands.sort(key=len,reverse=True)
    for comp in islands[1:]:bmesh.ops.delete(bm,geom=comp,context='VERTS')
    print('HAND volume %.1f cm3, %d island(s) dropped'%(abs(vol)*1e6,len(islands)-1))
    bm.to_mesh(me);bm.free()
    bpy.context.view_layer.objects.active=ob;ob.select_set(True)
    sm=ob.modifiers.new('Smooth','LAPLACIANSMOOTH');sm.lambda_factor=.5;sm.iterations=4;sm.use_volume_preserve=True;sm.use_normalized=True
    bpy.ops.object.modifier_apply(modifier=sm.name)
    # Even quads (a 3 mm voxel remesh): decimating the 0.8 mm surface left long slivers whose smooth shading drew faint
    # lines across the back of the hand, and even rings bend better under the bones. (QuadriFlow does nothing in -b.)
    ob.data.remesh_voxel_size=REMESH;ob.data.remesh_voxel_adaptivity=0
    bpy.ops.object.voxel_remesh()
    print('HAND remesh %.1f mm: %d faces'%(REMESH*1000,len(ob.data.polygons)))
    bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.delete_loose();bpy.ops.mesh.dissolve_degenerate(threshold=.0001);bpy.ops.mesh.normals_make_consistent(inside=False)
    bpy.ops.object.mode_set(mode='OBJECT')
    sm=ob.modifiers.new('Relax','LAPLACIANSMOOTH');sm.lambda_factor=.3;sm.iterations=2;sm.use_volume_preserve=True;sm.use_normalized=True
    bpy.ops.object.modifier_apply(modifier=sm.name)
    for p in ob.data.polygons:p.use_smooth=True
    return ob

def main():
    clear()
    J=rest_joints()
    ob=to_mesh(build_parts(J)) if '--capsules' in args else build_sdf(J)
    ao=rig(ob,J)
    print('HAND size',tuple(round(x,3) for x in ob.dimensions),'tip spread',round((J['IndexTip']-J['LittleTip']).length,3))
    export(ob,J)
    if PREVIEW:preview(ob,ao)

main()
