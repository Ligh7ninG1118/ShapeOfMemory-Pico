"""Dream kit for Shape of Memory (MemoryDream): abstract Mediterranean pieces (plaster walls with arched openings, stairs,
a portal frame, an arcade, a domed chapel, blue doors) that float in the dusk beyond the book. Each piece is modelled
in Blender with soft bevels, then lit once in Cycles and the light baked into one texture atlas, so in the game they
are unlit (one cheap material) yet show soft shadows, light bounced inside the arches and the dusk sky's colour.

The light is baked for the way the game shows them: every piece turns its front (-Y here, +Z in Unity) to the player,
and the sky's glow is beyond them, so the sun is low behind each piece and shines through its arches onto their inner
sides; the front takes the sky's lilac. A piece looks the same wherever it floats, which is why they all face the player.

Run (Blender 4.2, CPU; ~minutes):
  blender -b --factory-startup --python Tools/ShadowArt/build_dream_kit.py -- [--samples N] [--size PX] [--post-only]
Writes Assets/ShadowWorkshop/PicoFinal/Art/Dream/DreamKit.fbx and DreamKit.png, and a preview of the pieces as the game
draws them (unlit, against the sky) to <scratch>/dream_preview.png. --post-only redoes only the tone mapping and the
preview from the last raw bake (<scratch>/dream_raw.exr), for tuning EXPOSURE without baking again.
"""
import bpy, bmesh, math, os, sys
import numpy as np
from mathutils import Matrix, Vector

ROOT=os.path.abspath(os.path.join(os.path.dirname(__file__),'..','..'))
OUT=os.environ.get('DREAM_OUT',os.path.join(ROOT,'Assets','ShadowWorkshop','PicoFinal','Art','Dream'))
SCRATCH=os.environ.get('DREAM_SCRATCH',os.path.join(ROOT,'.agents','local','dream-kit'))
args=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
def arg(name,default):
    return type(default)(args[args.index(name)+1]) if name in args else default
SAMPLES=arg('--samples',256);SIZE=arg('--size',2048);POST_ONLY='--post-only' in args
# Tone mapping of the raw bake: exposure, then a soft shoulder over KNEE (below it, colours stay as baked).
EXPOSURE=arg('--exposure',1.0);KNEE=.78

def srgb_to_linear(c):
    c=c/255.0
    return c/12.92 if c<=.04045 else ((c+.055)/1.055)**2.4
def hexcolor(h):
    return tuple(srgb_to_linear(int(h[i:i+2],16)) for i in (0,2,4))+(1.0,)

# ---- Palette (sRGB): plasters and the Aegean blue of doors and a dome.
ALBEDO={
    'cream':'EADBC6','peach':'EBC8AE','sand':'DFC9A9','rose':'E6C2B8','white':'F0E8DD','stone':'D2BFA6','blue':'7FA8D6','door':'6A97C8',
}
# The dusk the pieces are lit by, matched to WindwardEveningSky as the game turns it (glow beyond the book, +Y here).
SKY={'zenith':'2F3170','upper':'5E4C8E','glow':'F2A08A','glowHigh':'C46F98','back':'7C6FA8','below':'B48AA6','under':'5A5080'}
SKY_STRENGTH=.65
# Suns (azimuth: where the light comes from, degrees from +Y toward +X; the player is at -Y): a warm key from the
# player's front left, so the fronts read and each arch's right side catches it, and the glow behind as a low pink rim
# that comes through the arches.
SUNS=[{'color':'FFE0C0','strength':3.2,'elevation':36.0,'azimuth':-125.0,'angle':6.0},
      {'color':'FF9A6E','strength':4.5,'elevation':9.0,'azimuth':30.0,'angle':4.0}]

# ---- Mesh helpers
def link(ob):
    bpy.context.scene.collection.objects.link(ob);return ob
def mesh_obj(name,bm):
    me=bpy.data.meshes.new(name);bm.normal_update();bm.to_mesh(me);bm.free()
    return link(bpy.data.objects.new(name,me))
def box(name,x0,x1,y0,y1,z0,z1,mat=None):
    bm=bmesh.new();bmesh.ops.create_cube(bm,size=1)
    for v in bm.verts:
        v.co=Vector((x0 if v.co.x<0 else x1,y0 if v.co.y<0 else y1,z0 if v.co.z<0 else z1))
    ob=mesh_obj(name,bm)
    if mat:ob.data.materials.append(mat)
    return ob
def prism(name,profile,axis,a0,a1,mat=None):
    """profile: 2D points; axis 'Y': (u,v)=(x,z) extruded along y; 'X': (u,v)=(y,z) along x."""
    bm=bmesh.new()
    def P(u,v,a):return (u,a,v) if axis=='Y' else (a,u,v)
    lo=[bm.verts.new(P(u,v,a0)) for u,v in profile];hi=[bm.verts.new(P(u,v,a1)) for u,v in profile]
    bm.faces.new(lo);bm.faces.new(hi[::-1]);n=len(profile)
    for i in range(n):
        j=(i+1)%n;bm.faces.new([lo[i],lo[j],hi[j],hi[i]])
    bmesh.ops.recalc_face_normals(bm,faces=bm.faces)
    ob=mesh_obj(name,bm)
    if mat:ob.data.materials.append(mat)
    return ob
def arch(cx,w,z0,spring,seg=28):
    """An arched opening's outline (x,z): straight sides from z0 up to the springline, a half circle over them."""
    r=w/2;pts=[(cx-r,z0),(cx+r,z0)]
    for k in range(seg+1):
        a=math.pi*k/seg;pts.append((cx+r*math.cos(a),spring+r*math.sin(a)))
    return pts
def circle(cx,cz,r,seg=40):
    return [(cx+r*math.cos(2*math.pi*k/seg),cz+r*math.sin(2*math.pi*k/seg)) for k in range(seg)]
def steps(n,run,rise,x0=0.0,soffit=.32,solid=False):
    """A flight's side outline (x,z): treads rising to the right, a sloped underside (solid: down to z=0)."""
    pts=[(x0,0.0)]
    for k in range(n):pts+=[(x0+k*run,(k+1)*rise),(x0+(k+1)*run,(k+1)*rise)]
    pts+=[(x0+n*run,0.0)] if solid else [(x0+n*run,n*rise-soffit),(x0+run*.9,0.0)]
    return pts
def door(cx,w,z0,spring,y,mat):  # an arched door panel 2 cm thick, its back at y
    return prism('door',arch(cx,w,z0,spring),'Y',y-.02,y,mat)
def evaluate(ob):
    dg=bpy.context.evaluated_depsgraph_get();me=bpy.data.meshes.new_from_object(ob.evaluated_get(dg),preserve_all_data_layers=True,depsgraph=dg)
    old=ob.data;ob.modifiers.clear();ob.data=me;bpy.data.meshes.remove(old)
def cut(target,*cutters):
    for c in cutters:
        m=target.modifiers.new('cut','BOOLEAN');m.operation='DIFFERENCE';m.solver='EXACT';m.object=c
        evaluate(target);bpy.data.objects.remove(c)
    return target
# Warm light inside each opening (baked only): it lights the opening's own sides, sill and arch from within, the
# glowing edges of the reference. Collected while a piece is built: (position, opening width, strength share).
GLOWS={};pending=[]
GLOW_COLOR='FFB27A';GLOW_POWER=9.0  # watts per square metre of opening width
def glow(profile,mid_y,share=1.0,axis='Y'):
    us=[p[0] for p in profile];vs=[p[1] for p in profile];u=(min(us)+max(us))/2;w=max(us)-min(us)
    v=(max(min(vs),0.0)+max(vs))/2
    pending.append((Vector((u,mid_y,v)) if axis=='Y' else Vector((mid_y,u,v)),w,share))
def through(profile,y0,y1):  # a cutter along Y, past both faces
    glow(profile,(y0+y1)/2);return prism('cutter',profile,'Y',y0-.2,y1+.2)
def recess(profile,front,depth):  # a cutter into the front face only; its glow at the mouth, soft
    glow(profile,front+min(.06,depth*.3),.3);return prism('cutter',profile,'Y',front-.2,front+depth)
def through_x(profile,x0,x1):
    glow(profile,(x0+x1)/2,axis='X');return prism('cutter',profile,'X',x0-.2,x1+.2)
def select_only(obs,active):
    bpy.ops.object.select_all(action='DESELECT')
    for o in obs:o.select_set(True)
    bpy.context.view_layer.objects.active=active
def join(name,parts):
    """One solid: the parts boolean-unioned (a plain join leaves touching faces inside, and the bevel then tears black
    slivers at those corners). Each part keeps its material."""
    base=parts[0]
    for p in parts[1:]:
        m=base.modifiers.new('fuse','BOOLEAN');m.operation='UNION';m.solver='EXACT';m.object=p;m.material_mode='TRANSFER'
        evaluate(base);bpy.data.objects.remove(p)
    base.name=name;base.data.name=name;return base
def finish(ob,bevel=.045):
    # Rounded edges with hardened normals: the flat faces stay flat (smoothing them into the bevel streaks the bake).
    for p in ob.data.polygons:p.use_smooth=True
    m=ob.modifiers.new('bevel','BEVEL');m.width=bevel;m.segments=3;m.limit_method='ANGLE';m.angle_limit=math.radians(30)
    m.use_clamp_overlap=True;m.harden_normals=True
    evaluate(ob)
    select_only([ob],ob);bpy.ops.object.origin_set(type='ORIGIN_GEOMETRY',center='BOUNDS')
    GLOWS[ob.name]=[(p-ob.location,w,s) for p,w,s in pending];pending.clear()
    return ob

# ---- The pieces. Front faces -Y (toward the player), bottom on z=0, metres.
def pieces(M):
    out=[]
    # Doorway with a cornice.
    w=cut(box('wall',-.9,.9,-.2,.2,0,2.8,M['cream']),through(arch(0,.9,-.3,1.75),-.2,.2))
    out.append(finish(join('Doorway',[w,box('cornice',-.98,.98,-.26,.26,2.8,2.93,M['stone'])])))
    # Tall slab: an arched window high up, a blind door below with a blue door in it.
    w=cut(box('wall',-.6,.6,-.18,.18,0,3.9,M['peach']),through(arch(0,.46,2.55,3.05),-.18,.18),recess(arch(0,.62,-.3,1.25),-.18,.12))
    out.append(finish(join('TallWindow',[w,door(0,.52,0,1.22,-.05,M['door'])])))
    # Arcade: three arches under a cornice.
    w=cut(box('wall',-2.1,2.1,-.22,.22,0,2.5,M['white']),*[through(arch(cx,.9,-.3,1.35),-.22,.22) for cx in (-1.3,0,1.3)])
    out.append(finish(join('Arcade',[w,box('cornice',-2.18,2.18,-.28,.28,2.5,2.62,M['sand'])])))
    # Portal: a slender free-standing frame.
    out.append(finish(cut(box('Portal',-.72,.72,-.15,.15,0,2.75,M['rose']),through(arch(0,1.06,-.3,1.95),-.15,.15)),bevel=.035))
    # Stair wall: a flight up the front to a landing under an arch.
    w=cut(box('wall',-1.25,1.25,-.2,.2,0,3.5,M['cream']),through(arch(.5,.72,1.5,2.25),-.2,.2))
    stair=prism('stairs',steps(5,.27,.3,x0=-1.25,solid=True),'Y',-.8,-.2,M['sand'])
    landing=box('landing',.1,.95,-.8,-.2,0,1.5,M['sand'])
    out.append(finish(join('StairWall',[w,stair,landing])))
    # Floating flight of stairs.
    out.append(finish(prism('Stairs',steps(6,.3,.24,x0=-.9),'Y',-.5,.5,M['peach'])))
    # Block with an arched niche and a blue door at its back.
    b=cut(box('block',-.8,.8,-.5,.5,0,1.8,M['sand']),recess(arch(0,.8,.32,1.05),-.5,.42))
    out.append(finish(join('Niche',[b,door(0,.66,.32,1.0,-.08,M['door']),box('sill',-.5,.5,-.62,-.5,.25,.32,M['stone'])])))
    # Chapel: a cube with a door niche under a blue dome.
    b=cut(box('cube',-.8,.8,-.8,.8,0,1.5,M['white']),recess(arch(0,.56,-.3,.85),-.8,.3))
    drum=link(bpy.data.objects.new('drum',bpy.data.meshes.new('drum')));bm=bmesh.new()
    bmesh.ops.create_cone(bm,cap_ends=True,segments=40,radius1=.7,radius2=.7,depth=.14);bm.to_mesh(drum.data);bm.free();drum.location=(0,0,1.57);drum.data.materials.append(M['white'])
    dome=link(bpy.data.objects.new('dome',bpy.data.meshes.new('dome')));bm=bmesh.new()
    bmesh.ops.create_uvsphere(bm,u_segments=40,v_segments=20,radius=.64)
    bmesh.ops.bisect_plane(bm,geom=bm.verts[:]+bm.edges[:]+bm.faces[:],plane_co=(0,0,0),plane_no=(0,0,1),clear_inner=True)
    bmesh.ops.holes_fill(bm,edges=[e for e in bm.edges if e.is_boundary]);bm.to_mesh(dome.data);bm.free();dome.location=(0,0,1.64);dome.data.materials.append(M['blue'])
    for o in (drum,dome):select_only([o],o);bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    out.append(finish(join('Chapel',[b,drum,dome,door(0,.46,0,.82,-.5,M['door'])])))
    # Tower: two windows through it, a blue door at its foot, a cap.
    t=cut(box('tower',-.5,.5,-.5,.5,0,4.6,M['rose']),through(arch(0,.34,3.3,3.72),-.5,.5),through(arch(0,.3,2.35,2.7),-.5,.5),recess(arch(0,.5,-.3,.82),-.5,.25))
    out.append(finish(join('Tower',[t,box('cap',-.58,.58,-.58,.58,4.6,4.74,M['stone']),door(0,.42,0,.8,-.25,M['door'])])))
    # Wall with a round window on a step.
    w=cut(box('wall',-.85,.85,-.17,.17,.18,2.1,M['peach']),through(circle(0,1.25,.38),-.17,.17))
    out.append(finish(join('Oculus',[w,box('step',-1.0,1.0,-.32,.32,0,.18,M['stone'])])))
    # Corner: two arched walls meeting, the corner turned to the player.
    a=cut(box('a',-1.5,.15,-.15,.15,0,2.6,M['cream']),through(arch(-.7,.72,-.3,1.5),-.15,.15))
    b=cut(box('b',-.15,.15,.15,1.5,0,2.6,M['cream']),through_x(arch(.82,.6,1.1,1.75),-.15,.15))
    c=join('Corner',[a,b]);turn=Matrix.Rotation(math.radians(-45),4,'Z');c.matrix_world=turn@c.matrix_world
    select_only([c],c);bpy.ops.object.transform_apply(rotation=True)
    pending[:]=[(turn@p,w,s) for p,w,s in pending]
    out.append(finish(c))
    # Gate: an arch on a stepped plinth.
    g=cut(box('gate',-.75,.75,-.15,.15,.36,2.75,M['white']),through(arch(0,.88,.2,1.9),-.15,.15))
    out.append(finish(join('Gate',[g,box('plinth',-1.3,1.3,-.95,.95,0,.18,M['stone']),box('plinth2',-1.0,1.0,-.65,.65,.18,.36,M['sand'])])))
    # Bridge: a deck over one wide arch, low parapets along it.
    b=cut(box('bridge',-2.3,2.3,-.45,.45,0,1.45,M['sand']),through(arch(0,3.3,-.6,-.45,seg=40),-.45,.45))
    out.append(finish(join('Bridge',[b,box('parapetA',-2.3,2.3,-.45,-.33,1.45,1.72,M['cream']),box('parapetB',-2.3,2.3,.33,.45,1.45,1.72,M['cream'])])))
    # Column: a round shaft between a square base and capital.
    shaft=link(bpy.data.objects.new('shaft',bpy.data.meshes.new('shaft')));bm=bmesh.new()
    bmesh.ops.create_cone(bm,cap_ends=True,segments=40,radius1=.22,radius2=.2,depth=2.4);bm.to_mesh(shaft.data);bm.free();shaft.location=(0,0,1.4);shaft.data.materials.append(M['white'])
    select_only([shaft],shaft);bpy.ops.object.transform_apply(location=True)
    out.append(finish(join('Column',[shaft,box('base',-.34,.34,-.34,.34,0,.2,M['stone']),box('capital',-.36,.36,-.36,.36,2.6,2.82,M['stone'])]),bevel=.03))
    return out

# ---- Light
def world():
    w=bpy.data.worlds.new('Dusk');bpy.context.scene.world=w;w.use_nodes=True;nt=w.node_tree;nt.nodes.clear()
    N=lambda t,**k:nt.nodes.new(t)
    out=N('ShaderNodeOutputWorld');bg=N('ShaderNodeBackground');bg.inputs['Strength'].default_value=SKY_STRENGTH
    tc=N('ShaderNodeTexCoord');sep=N('ShaderNodeSeparateXYZ');nt.links.new(tc.outputs['Generated'],sep.inputs[0])
    def mapr(src,a,b):
        m=N('ShaderNodeMapRange');m.clamp=True;m.inputs['From Min'].default_value=a;m.inputs['From Max'].default_value=b
        nt.links.new(src,m.inputs['Value']);return m.outputs['Result']
    def mix(fac,c1,c2):
        m=N('ShaderNodeMix');m.data_type='RGBA';nt.links.new(fac,m.inputs[0])
        slots=[s for s in m.inputs if s.type=='RGBA']
        for slot,c in zip(slots,(c1,c2)):
            if isinstance(c,str):slot.default_value=hexcolor(c)
            else:nt.links.new(c,slot)
        return [s for s in m.outputs if s.type=='RGBA'][0]
    x,y,z=sep.outputs['X'],sep.outputs['Y'],sep.outputs['Z']
    # Round the horizon: the glow beyond (+Y), lilac behind the player (-Y).
    horizon=mix(mapr(y,-.6,.9),SKY['back'],SKY['glow'])
    high=mix(mapr(y,-.4,1.0),SKY['upper'],SKY['glowHigh'])
    above=mix(mapr(z,0.0,.35),horizon,high)
    above=mix(mapr(z,.35,.95),above,SKY['zenith'])
    below=mix(mapr(z,0.0,-.25),horizon,SKY['below'])
    below=mix(mapr(z,-.25,-.9),below,SKY['under'])
    is_above=mapr(z,-.01,.01)
    col=mix(is_above,below,above)
    nt.links.new(col,bg.inputs['Color']);nt.links.new(bg.outputs[0],out.inputs[0])
def sun():
    for i,S in enumerate(SUNS):
        d=bpy.data.lights.new(f'Sun{i}','SUN');d.energy=S['strength'];d.color=hexcolor(S['color'])[:3];d.angle=math.radians(S['angle'])
        s=link(bpy.data.objects.new(f'Sun{i}',d))
        el,az=math.radians(S['elevation']),math.radians(S['azimuth'])
        toward=-Vector((math.sin(az)*math.cos(el),math.cos(az)*math.cos(el),math.sin(el)))
        s.rotation_euler=toward.to_track_quat('-Z','Y').to_euler()

def materials():
    M={}
    for name,h in ALBEDO.items():
        m=bpy.data.materials.new(name);m.use_nodes=True;nt=m.node_tree;nt.nodes.clear()
        d=nt.nodes.new('ShaderNodeBsdfDiffuse');d.inputs['Color'].default_value=hexcolor(h);d.inputs['Roughness'].default_value=1.0
        o=nt.nodes.new('ShaderNodeOutputMaterial');nt.links.new(d.outputs[0],o.inputs[0]);M[name]=m
    return M

def shelf_pack(sides):
    """Squares (relative sides) in rows inside the unit square, as big as fit: (x, y, side) each."""
    order=sorted(range(len(sides)),key=lambda i:-sides[i])
    def place(k):
        x=y=row=0.0;out=[None]*len(sides)
        for i in order:
            s=sides[i]*k
            if x+s>1+1e-9:x=0.0;y+=row;row=0.0
            out[i]=(x,y,s);x+=s;row=max(row,s)
        return out,y+row
    lo,hi=0.0,1.0/max(sides)
    for _ in range(50):
        k=(lo+hi)/2
        if place(k)[1]<=1:lo=k
        else:hi=k
    return place(lo)[0]
def unwrap(obs):
    """Each piece unwrapped and packed on its own, then given a tile of the atlas sized by its surface (even density).
    (Packing them all at once in one multi-object edit left each piece filling the whole atlas, overlapping.)"""
    bpy.context.scene.tool_settings.use_uv_select_sync=True
    tiles=shelf_pack([math.sqrt(sum(p.area for p in o.data.polygons)) for o in obs])
    gap=3.0/SIZE  # pixels between tiles
    for o,(x,y,side) in zip(obs,tiles):
        select_only([o],o);bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT')
        bpy.ops.uv.smart_project(angle_limit=math.radians(60),island_margin=0.0,area_weight=0.0,correct_aspect=True,scale_to_bounds=False)
        bpy.ops.uv.pack_islands(rotate=True,margin_method='FRACTION',margin=6.0/(side*SIZE))
        bpy.ops.object.mode_set(mode='OBJECT')
        uv=np.empty(len(o.data.loops)*2,np.float32);o.data.uv_layers.active.data.foreach_get('uv',uv);uv=uv.reshape(-1,2)
        inner=side-2*gap;uv=uv*inner+np.array([x+gap,y+gap],np.float32)
        o.data.uv_layers.active.data.foreach_set('uv',uv.ravel())
    print('DREAM atlas tiles:',', '.join(f'{o.name} {t[2]*SIZE:.0f}px' for o,t in zip(obs,tiles)))

def bake(obs,M):
    sc=bpy.context.scene;sc.render.engine='CYCLES';sc.cycles.device='CPU';sc.cycles.samples=SAMPLES;sc.cycles.use_denoising=False
    sc.cycles.max_bounces=6;sc.cycles.diffuse_bounces=4;sc.cycles.use_adaptive_sampling=False
    sc.render.bake.margin=24;sc.render.bake.margin_type='EXTEND';sc.render.bake.use_clear=True
    img=bpy.data.images.new('dream_raw',SIZE,SIZE,alpha=False,float_buffer=True)
    for m in M.values():
        n=m.node_tree.nodes.new('ShaderNodeTexImage');n.image=img;m.node_tree.nodes.active=n
    # Far apart, so no piece shades or lights another; each opening's glow with its piece.
    for i,o in enumerate(obs):
        o.location=(i*45.0,0,o.location.z)
        for k,(rel,w,share) in enumerate(GLOWS.get(o.name,[])):
            d=bpy.data.lights.new(f'glow_{o.name}_{k}','POINT');d.energy=GLOW_POWER*w*w*share;d.color=hexcolor(GLOW_COLOR)[:3];d.shadow_soft_size=w*.22
            link(bpy.data.objects.new(d.name,d)).location=o.location+rel
    select_only(obs,obs[0])
    bpy.ops.object.bake(type='COMBINED',use_clear=True,margin=24)
    img.filepath_raw=os.path.join(SCRATCH,'dream_raw.exr');img.file_format='OPEN_EXR';img.save()
    return img

def post(raw):
    """Tone map the raw (linear) bake into the sRGB atlas the game uses unlit."""
    w,h=raw.size;buf=np.empty(w*h*4,np.float32);raw.pixels.foreach_get(buf);px=buf.reshape(h,w,4)[:,:,:3]*EXPOSURE
    over=np.maximum(px-KNEE,0);px=np.where(px>KNEE,KNEE+(1-KNEE)*(1-np.exp(-over/(1-KNEE))),px)
    px=np.clip(px,0,1);px=np.where(px<=.0031308,px*12.92,1.055*np.power(px,1/2.4)-.055)
    px=np.clip(px+(np.random.default_rng(1).random(px.shape,dtype=np.float32)-.5)/255.0,0,1)  # dither the 8-bit steps
    out=bpy.data.images.new('DreamKit',w,h,alpha=False);out.colorspace_settings.name='Non-Color'
    rgba=np.ones((h,w,4),np.float32);rgba[:,:,:3]=px;out.pixels.foreach_set(rgba.ravel())
    out.filepath_raw=os.path.join(OUT,'DreamKit.png');out.file_format='PNG';out.save()
    return out

def export(obs):
    for o in obs:o.location=(0,0,0)
    select_only(obs,obs[0])
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT,'DreamKit.fbx'),use_selection=True,object_types={'MESH'},apply_unit_scale=True,
        apply_scale_options='FBX_SCALE_ALL',axis_forward='-Z',axis_up='Y',bake_space_transform=True,mesh_smooth_type='FACE',
        use_mesh_modifiers=True,path_mode='STRIP',embed_textures=False,add_leaf_bones=False,bake_anim=False)

def preview(obs):
    """The pieces as the game draws them: unlit atlas, floating against the sky, from the player's eye."""
    atlas=bpy.data.images.load(os.path.join(OUT,'DreamKit.png'),check_existing=False);atlas.colorspace_settings.name='sRGB'
    m=bpy.data.materials.new('unlit');m.use_nodes=True;nt=m.node_tree;nt.nodes.clear()
    t=nt.nodes.new('ShaderNodeTexImage');t.image=atlas;t.interpolation='Cubic';e=nt.nodes.new('ShaderNodeEmission');o=nt.nodes.new('ShaderNodeOutputMaterial')
    nt.links.new(t.outputs[0],e.inputs[0]);nt.links.new(e.outputs[0],o.inputs[0])
    sc=bpy.context.scene;sc.view_settings.view_transform='Standard';sc.view_settings.look='None';sc.cycles.samples=24
    for l in [o for o in sc.objects if o.type=='LIGHT']:l.hide_render=True
    rng=np.random.default_rng(3);n=len(obs)
    for i,src in enumerate(obs):
        me=src.data.copy();me.materials.clear();me.materials.append(m)
        for p in me.polygons:p.material_index=0
        # Spread across the view, each 9-15 degrees tall, alternating high and low.
        size=max(src.dimensions);d=size/math.tan(math.radians(float(rng.uniform(9,15))))
        az=math.radians(-52+104*(i+.5)/n+float(rng.uniform(-3,3)));el=math.radians((14 if i%2 else -2)+float(rng.uniform(-4,4)))
        pos=Vector((math.sin(az)*math.cos(el),math.cos(az)*math.cos(el),math.sin(el)))*d
        # Facing the eye, then turned 15-35 degrees either way (as MemoryDream turns them), so the arches' sides show.
        turn=math.radians(float(rng.uniform(15,35)))*(1 if rng.random()<.5 else -1)
        ob=link(bpy.data.objects.new('view_'+src.name,me));ob.location=pos;ob.rotation_euler=(0,0,math.atan2(-pos.x,pos.y)+turn)
    cam=link(bpy.data.objects.new('cam',bpy.data.cameras.new('cam')));cam.data.angle=math.radians(100);cam.rotation_euler=(math.radians(92),0,0)
    sc.camera=cam;sc.render.resolution_x=1600;sc.render.resolution_y=800;sc.render.filepath=os.path.join(SCRATCH,'dream_preview.png')
    for o in obs:o.hide_render=True
    bpy.ops.render.render(write_still=True)
    # Contact sheet: each piece alone, turned 25 degrees, in a grid (orthographic), for checking the geometry and bake.
    views=[o for o in sc.objects if o.name.startswith('view_')];cols=5
    for i,o in enumerate(views):
        o.location=((i%cols)*6.0,40.0,-(i//cols)*6.0);o.rotation_euler=(0,0,math.radians(25 if i%2 else -25))
    cam.data.type='ORTHO';cam.data.ortho_scale=cols*6.0;cam.rotation_euler=(math.radians(90),0,0)
    rows=(len(views)+cols-1)//cols;cam.location=((cols-1)*3.0,0,-(rows-1)*3.0)
    sc.render.resolution_x=2000;sc.render.resolution_y=int(2000*rows/cols);sc.render.filepath=os.path.join(SCRATCH,'dream_sheet.png')
    bpy.ops.render.render(write_still=True)

def main():
    os.makedirs(OUT,exist_ok=True);os.makedirs(SCRATCH,exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    M=materials();obs=pieces(M);world();sun()
    print('DREAM pieces:',', '.join(f'{o.name} {o.dimensions.x:.2f}x{o.dimensions.z:.2f}x{o.dimensions.y:.2f} m {len(o.data.polygons)} faces' for o in obs))
    unwrap(obs)
    raw=bpy.data.images.load(os.path.join(SCRATCH,'dream_raw.exr')) if POST_ONLY else bake(obs,M)
    post(raw);export(obs);preview(obs)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(SCRATCH,'dream_kit.blend'))
    print('DREAM done')
main()
