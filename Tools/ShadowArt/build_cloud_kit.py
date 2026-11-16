"""Cloud kit for Shape of Memory (MemoryClouds): stylised cumulus that the player stands among, from a flat cloud sea
to big banks that frame the view. Shaded in the game by ShapeOfMemory/Cloud (a colour ramp lit by the sky's glow, haze
toward the sky), so here they are geometry only, plus two things the shader reads from the vertex colour:
  R  crevice occlusion (1 open, 0 deep in a fold between puffs), from rays cast over each vertex's hemisphere
  G  height in the cloud (0 base, 1 top)
Each cloud is a few domes (half superellipsoids), each filled by a bed of big balls that reaches up to its surface and
covered with smaller balls that poke out of it (big low down, smaller near the top: the cauliflower), spread evenly so
no pit shows. Blender polygonises the balls as one metaball union, which is then squashed flat at the base and scaled
(uniformly) to its height. Normals: first the dense surface's own (so decimation leaves no facets in the shading), then
mixed toward an inflated, smoothed copy of the same balls, so light reads as one soft mass with lobes rather than as a
heap of spheres, while the silhouette keeps its lumps.
Every cloud is exported three times: <name> (near), <name>_LOD1 (mid distance) and <name>_LOD2 (far).
Towers were tried and dropped (2026-11-12): at mid distance they read as stone pillars.

Run (Blender 4.2, CPU; about a minute):
  blender -b --factory-startup --python Tools/ShadowArt/build_cloud_kit.py -- [--preview] [--seed N]
Writes Assets/ShadowWorkshop/PicoFinal/Art/Clouds/CloudKit.fbx; --preview also renders a contact sheet of the near
meshes (Workbench) to <scratch>/cloud_kit_preview.png.
"""
import bpy, math, os, sys
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from mathutils.kdtree import KDTree

ROOT=os.path.abspath(os.path.join(os.path.dirname(__file__),'..','..'))
OUT=os.environ.get('CLOUD_OUT',os.path.join(ROOT,'Assets','ShadowWorkshop','PicoFinal','Art','Clouds'))
SCRATCH=os.environ.get('CLOUD_SCRATCH',os.path.join(ROOT,'.agents','local','cloud-kit'))
args=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
def arg(name,default):
    return type(default)(args[args.index(name)+1]) if name in args else default
SEED=arg('--seed',11);PREVIEW='--preview' in args

# How far the normals lean toward the inflated copy's (0 real, 1 fully smoothed), per mesh (near, _LOD1, _LOD2), and how
# much bigger that copy's balls are. The lighter meshes lean further: their big triangles would show as facets in the
# shading, and lobes that small don't read at their distance anyway.
NORMAL_MIX=(.55,.75,.9);PROXY_GROW=1.7
# Base squash: below BASE of the height, the cloud is pressed flat by SQUASH (cumulus have flat bottoms).
BASE=.16;SQUASH=.3
# Lumps: a ball pokes POKE of its radius out of its dome; neighbours keep SPACING of their mean radius apart.
POKE=.5;SPACING=.64
# Occlusion rays per vertex, and how far they look (share of the cloud's height).
AO_RAYS=24;AO_REACH=.4

# Domes, each (x, y, half width, half depth, height, sides[, raised base]) in metres: sides 2 is a round dome, higher
# gives straighter sides. lump: ball radius at the base as a share of the dome's smaller half side; top: the radius
# near the top as a share of the base one. tris: near / mid / far triangle budgets.
def sea(w,d,h,n,rng):
    domes=[]
    for _ in range(n):
        u,v=rng.uniform(-1,1,2)
        while u*u+v*v>1:u,v=rng.uniform(-1,1,2)
        a=w*rng.uniform(.18,.3);b=d*rng.uniform(.2,.34)
        domes.append((u*(w*.5-a),v*(d*.5-b),a,b,h*rng.uniform(.45,1),2.2))
    return domes
def row(w,d,h,n,rng,sides=2.6):
    domes=[]
    for i in range(n):
        x=-w*.5+w*(i+.5)/n+rng.uniform(-.3,.3)*w/n
        a=w/n*rng.uniform(.7,.95);b=d*.5*rng.uniform(.75,1)
        hh=h*(1-.45*abs(x)/(w*.5))*rng.uniform(.7,1)
        domes.append((x,rng.uniform(-.15,.15)*d,a,b,hh,sides))
    return domes
# The sea as a few big soft mounds, lumped like the puffs (2026-11-14: the user liked the puffs' few big round lobes
# better than the old sea's many small lumps, and asked for the sea to look like them): fewer, wider domes than sea(),
# big lumps (SOFT_SEA_LUMP of the dome, as on a puff) that shrink little toward the top.
SOFT_SEA_LUMP=.95;SOFT_SEA_TOP=.8
def soft_sea(w,d,h,n,rng):
    domes=[]
    for _ in range(n):
        u,v=rng.uniform(-1,1,2)
        while u*u+v*v>1:u,v=rng.uniform(-1,1,2)
        a=w*rng.uniform(.26,.36);b=d*rng.uniform(.28,.4)
        domes.append((u*(w*.5-a),v*(d*.5-b),a,b,h*rng.uniform(.55,1),2.2))
    return domes
def puff(w,d,h,rng,side=2):
    domes=[(0,0,w*.36,d*.4,h,2.4)]
    for i in range(side):
        s=-1 if i%2==0 else 1
        domes.append((s*w*rng.uniform(.22,.3),rng.uniform(-.15,.15)*d,w*rng.uniform(.2,.26),d*rng.uniform(.28,.34),h*rng.uniform(.45,.65),2.2))
    return domes

# The sea pieces are soft_sea() domes from their own generator (sea_rng). The old sea() domes are still drawn from rng and
# their balls built and dropped (build: 'stream'), so every other cloud gets the same random numbers as before and comes
# out exactly as it was. 'footprint' (metres, x by y) is each sea piece's old size, which the new one is stretched to, so
# the scenes that lay the sea out (MemoryClouds, the environment scene's Cloud_Sea) still close up.
def soft(old,new,size,footprint,n,rng,sea_rng,tris):
    w,d,h=size
    return dict(domes=soft_sea(w,d,h,n,sea_rng),size=size,lump=SOFT_SEA_LUMP,top=SOFT_SEA_TOP,tris=tris,
                stream=dict(domes=old,lump=new[0],top=new[1]),footprint=footprint)
def specs(rng):
    old=[sea(30,22,4.5,7,rng),sea(26,26,4,7,rng),sea(34,16,5.5,6,rng),sea(22,18,5,5,rng)]
    return {
        'Sea_A':  soft(old[0],(.62,.65),(30,22,4.5),(17.84,15.83),4,rng,sea_rng,(3000,1000,320)),
        'Sea_B':  soft(old[1],(.62,.65),(26,26,4),(14.39,19.05),4,rng,sea_rng,(3000,1000,320)),
        'Sea_C':  soft(old[2],(.58,.65),(34,16,5.5),(19.97,11.92),4,rng,sea_rng,(3000,1000,320)),
        'Sea_D':  soft(old[3],(.62,.65),(22,18,5),(15.15,13.95),3,rng,sea_rng,(2800,950,300)),
        'Puff_A': dict(domes=puff(10,8,8,rng),size=(10,8,8),lump=.56,top=.65,tris=(2400,600,260)),
        'Puff_B': dict(domes=puff(12,9,9,rng,3),size=(12,9,9),lump=.56,top=.65,tris=(2600,650,280)),
        'Puff_C': dict(domes=puff(8,7,6,rng,1),size=(8,7,6),lump=.58,top=.65,tris=(2000,500,220)),
        'Puff_D': dict(domes=puff(14,10,10,rng,2),size=(14,10,10),lump=.55,top=.65,tris=(2800,700,300)),
        'Bank_A': dict(domes=row(44,14,12,5,rng),size=(44,14,12),lump=.56,top=.65,tris=(3400,900,380)),
        'Bank_B': dict(domes=row(36,12,10,4,rng),size=(36,12,10),lump=.56,top=.65,tris=(3200,850,360)),
    }

def clear():
    for o in list(bpy.data.objects):bpy.data.objects.remove(o,do_unlink=True)
    for m in list(bpy.data.meshes):bpy.data.meshes.remove(m)
    for m in list(bpy.data.metaballs):bpy.data.metaballs.remove(m)

# ---- Balls over the domes
def dome(dm):
    return dm if len(dm)==7 else (*dm,0.0)
def dome_f(dm,p):
    """< 1 inside the dome, 1 on its surface (below its raised base it continues straight down)."""
    x,y,a,b,h,q,z0=dome(dm)
    return ((p[0]-x)/a)**2+((p[1]-y)/b)**2+(max(p[2]-z0,0)/h)**q

def halton(i,base):
    f,r=1.0,0.0
    while i>0:
        f/=base;r+=f*(i%base);i//=base
    return r

def balls_for(rng,spec):
    domes=spec['domes'];balls=[]
    for dm in domes:
        x,y,a,b,h,q,z0=dome(dm);rb=spec['lump']*min(a,b,h*1.2);rt=rb*spec['top']
        # Core: big balls filling most of the dome (along its longer side, never above its top), so no hollow shows
        # between the lumps.
        long_a=a>=b;ratio=max(a,b)/min(a,b)
        for k in range(6):
            z=h*(.06+.15*k);s=(1-(z/h)**q)**.5
            if s<.2:continue
            along=max(1,int(round(ratio*1.2)))
            for j in range(along):
                u=0 if along==1 else (j/(along-1)-.5)*1.3
                cx,cy=(x+u*a*s,y) if long_a else (x,y+u*b*s)
                balls.append((Vector((cx,cy,z0+z)),.9*min(min(a,b)*s,h-z)))
        # Bed: big balls over the whole footprint, each topping out just under the dome's surface above it, so a wide,
        # low dome (the sea's) has no pit between its lumps.
        sp=.7*min(a,b,h*1.5);nx=int(math.ceil(a/sp));ny=int(math.ceil(b/sp))
        for i in range(-nx,nx+1):
            for j in range(-ny,ny+1):
                rr=(i*sp/a)**2+(j*sp/b)**2
                if rr>.75:continue
                top=h*(1-rr)**(1/q);r=min(.55*top,.8*min(a,b)*math.sqrt(1-rr))
                if r<.15*h:continue
                balls.append((Vector((x+i*sp,y+j*sp,z0+top-1.05*r)),r))
        # Lumps: candidates spread evenly over the surface (a Halton sequence: z in proportion to the girth there, round
        # the dome), kept if clear of the others. Even spread leaves no gap for a pit; lumps inside a neighbouring dome
        # stay, as they fill the crease where the two meet.
        accepted=[];target=int(2.1*(a*b+h*(a+b))/(rb*rb))+6;start=int(rng.integers(1,5000))
        for k in range(start,start+target*40):
            if len(accepted)>=target:break
            z=h*halton(k,2);girth=(1-(z/h)**q)**.5
            if halton(k,5)>girth+.15:continue
            th=2*math.pi*halton(k,3)
            px,py=x+a*girth*math.cos(th),y+b*girth*math.sin(th)
            p=Vector((px,py,z0+z))
            g=Vector(((px-x)/(a*a),(py-y)/(b*b),q*(z**(q-1))/(h**q) if z>0 else 0)).normalized()
            r=(rb+(rt-rb)*(z/h))*rng.uniform(.8,1.2)
            c=p-g*(r*(1-POKE))
            if any((c-o).length<SPACING*.5*(r+orr) for o,orr in accepted):continue
            accepted.append((c,r))
        balls+=accepted
    return balls

def polygonise(name,balls,resolution,grow_by=1.0):
    mb=bpy.data.metaballs.new(name);mb.resolution=resolution;mb.render_resolution=resolution;mb.threshold=.6
    for c,r in balls:
        e=mb.elements.new();e.co=c;e.radius=r*grow_by;e.stiffness=2.0
    ob=bpy.data.objects.new(name,mb);bpy.context.scene.collection.objects.link(ob)
    dg=bpy.context.evaluated_depsgraph_get();dg.update()
    me=bpy.data.meshes.new_from_object(ob.evaluated_get(dg),preserve_all_data_layers=True,depsgraph=dg)
    me.name=name+'_raw'
    bpy.data.objects.remove(ob,do_unlink=True);bpy.data.metaballs.remove(mb)
    if len(me.vertices)==0:raise RuntimeError(f'{name}: metaball polygonised to nothing')
    return me

def coords(me):
    co=np.empty(len(me.vertices)*3,np.float32);me.vertices.foreach_get('co',co);return co.reshape(-1,3)
def set_coords(me,co):
    me.vertices.foreach_set('co',co.astype(np.float32).ravel());me.update()

def frame_of(co,h):
    lo,hi=co.min(0),co.max(0);mid=(lo+hi)*.5
    zmin=BASE-BASE*SQUASH
    return lo[2],hi[2]-lo[2],mid,h/((hi[2]-lo[2])*(1-zmin)),(1.0,1.0)
def fit(co,frame):
    """Squash the base flat, then one scale for all axes so the height is the spec's, base at z 0, middle over the origin
    (then x and y stretched by the frame's last entry: a sea piece onto its old footprint)."""
    z0,zh,mid,s,(sx,sy)=frame
    zr=(co[:,2]-z0)/zh;low=zr<BASE;zr[low]=BASE-(BASE-zr[low])*SQUASH
    zmin=BASE-BASE*SQUASH
    out=np.empty_like(co)
    out[:,0]=(co[:,0]-mid[0])*s*sx;out[:,1]=(co[:,1]-mid[1])*s*sy;out[:,2]=(zr-zmin)*zh*s
    return out

def mesh_object(name,me):
    ob=bpy.data.objects.new(name,me);bpy.context.scene.collection.objects.link(ob);return ob

def evaluated(ob):
    dg=bpy.context.evaluated_depsgraph_get();dg.update()
    return bpy.data.meshes.new_from_object(ob.evaluated_get(dg),preserve_all_data_layers=True,depsgraph=dg)

def decimate(name,me,target):
    ob=mesh_object(name+'_dec',me.copy())
    tris=sum(len(p.vertices)-2 for p in me.polygons)
    if tris>target:
        m=ob.modifiers.new('dec','DECIMATE');m.ratio=target/tris;m.use_collapse_triangulate=True
    out=evaluated(ob);bpy.data.objects.remove(ob,do_unlink=True);return out

def smooth_normals(name,me,dense_ob,proxy_ob,mix_toward_proxy):
    """The dense surface's normals (exact, smooth), then mix_toward_proxy of the way to the inflated proxy's."""
    me.shade_smooth()
    ob=mesh_object(name+'_nrm',me)
    for source,mix in ((dense_ob,1.0),(proxy_ob,mix_toward_proxy)):
        m=ob.modifiers.new('normals','DATA_TRANSFER');m.object=source
        m.use_loop_data=True;m.data_types_loops={'CUSTOM_NORMAL'};m.loop_mapping='POLYINTERP_NEAREST'
        m.mix_mode='REPLACE';m.mix_factor=mix
    out=evaluated(ob);bpy.data.objects.remove(ob,do_unlink=True);return out

def hemisphere(n):
    """Cosine-weighted directions around +Z (Fibonacci spiral)."""
    dirs=[];g=math.pi*(3-math.sqrt(5))
    for i in range(n):
        u=(i+.5)/n;r=math.sqrt(u);t=g*i
        dirs.append(Vector((r*math.cos(t),r*math.sin(t),math.sqrt(max(0,1-u)))))
    return dirs

def occlusion(me,bvh,height):
    dirs=hemisphere(AO_RAYS);reach=AO_REACH*height
    occ=np.ones(len(me.vertices),np.float32)
    for i,v in enumerate(me.vertices):
        nrm=v.normal;basis=nrm.to_track_quat('Z','Y').to_matrix()
        origin=v.co+nrm*(.003*height);hits=0
        for d in dirs:
            if bvh.ray_cast(origin,basis@d,reach)[0] is not None:hits+=1
        occ[i]=1-hits/len(dirs)
    return occ**.8

def write_colours(me,occ):
    co=coords(me);n=len(co)
    z=co[:,2];h=(z-z.min())/max(z.max()-z.min(),1e-6)
    attr=me.color_attributes.new(name='Col',type='FLOAT_COLOR',domain='POINT')
    attr.data.foreach_set('color',np.stack([occ,h,np.ones(n),np.ones(n)],1).astype(np.float32).ravel())
    me.color_attributes.active_color=attr

def build(name,spec):
    w,d,h=spec['size']
    if 'stream' in spec:balls_for(rng,spec['stream'])   # the old sea's draws, dropped (see specs)
    balls=balls_for(sea_rng if 'stream' in spec else rng,spec)
    smallest=min(r for _,r in balls)
    res=max(smallest*.45,h/60,.12)
    raw=polygonise(name,balls,res)
    frame=frame_of(coords(raw),h)
    if 'footprint' in spec:
        ext=fit(coords(raw),frame);ext=ext.max(0)-ext.min(0)
        frame=frame[:4]+((spec['footprint'][0]/ext[0],spec['footprint'][1]/ext[1]),)
        print(f'CLOUD {name}: stretched {frame[4][0]:.2f} x {frame[4][1]:.2f} onto its footprint')
    set_coords(raw,fit(coords(raw),frame))
    # Normals from the same balls grown fatter and smoothed: the soft mass under the lobes. Same frame as the cloud,
    # so it sits round it and nearest-face lookups land on its outside.
    proxy=polygonise(name+'_proxy',balls,res*1.8,PROXY_GROW);set_coords(proxy,fit(coords(proxy),frame))
    pob=mesh_object(name+'_proxy',proxy)
    sm=pob.modifiers.new('smooth','SMOOTH');sm.factor=1.0;sm.iterations=15
    sproxy=evaluated(pob);bpy.data.objects.remove(pob,do_unlink=True)
    pob=mesh_object(name+'_proxy_s',sproxy)
    dense=raw.copy();dense.shade_smooth();dob=mesh_object(name+'_dense',dense)
    near=decimate(name,raw,spec['tris'][0])
    bvh=BVHTree.FromPolygons([v.co.copy() for v in near.vertices],[tuple(p.vertices) for p in near.polygons])
    occ_near=occlusion(near,bvh,h)
    kd=KDTree(len(near.vertices))
    for i,v in enumerate(near.vertices):kd.insert(v.co,i)
    kd.balance()
    out=[]
    for tier,(suffix,target) in enumerate(zip(('','_LOD1','_LOD2'),spec['tris'])):
        me=near if suffix=='' else decimate(name+suffix,raw,target)
        # The lighter meshes take the near mesh's occlusion (their surfaces sit inside the near one's in places, so rays
        # cast from them would hit the wrong thing).
        occ=occ_near if suffix=='' else np.array([occ_near[kd.find(v.co)[1]] for v in me.vertices],np.float32)
        me=smooth_normals(name+suffix,me,dob,pob,NORMAL_MIX[tier])
        write_colours(me,occ)
        me.name=name+suffix
        ob=mesh_object(name+suffix,me);out.append(ob)
        tris=sum(len(p.vertices)-2 for p in me.polygons);dims=ob.dimensions
        print(f'CLOUD {name+suffix}: {len(balls)} balls, res {res:.2f}, {tris} tris, {dims.x:.1f} x {dims.y:.1f} x {dims.z:.1f} m, open {occ.mean():.2f}')
    bpy.data.objects.remove(pob,do_unlink=True);bpy.data.objects.remove(dob,do_unlink=True)
    return out

def export(obs):
    os.makedirs(OUT,exist_ok=True)
    bpy.ops.object.select_all(action='DESELECT')
    for o in obs:o.select_set(True)
    bpy.context.view_layer.objects.active=obs[0]
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT,'CloudKit.fbx'),use_selection=True,object_types={'MESH'},apply_unit_scale=True,
        apply_scale_options='FBX_SCALE_ALL',axis_forward='-Z',axis_up='Y',bake_space_transform=True,mesh_smooth_type='OFF',
        use_mesh_modifiers=False,use_triangles=True,colors_type='LINEAR',path_mode='STRIP',embed_textures=False,add_leaf_bones=False,bake_anim=False)

def preview(obs):
    """Workbench contact sheet of the near meshes, from a little above (as the player mostly sees them)."""
    for o in obs:o.hide_render='_LOD' in o.name
    near=[o for o in obs if '_LOD' not in o.name]
    x=0.0
    for o in near:
        w=o.dimensions.x;o.location=(x+w*.5,0,0);x+=w+4
    sc=bpy.context.scene;sc.render.engine='BLENDER_WORKBENCH'
    sc.display.shading.light='STUDIO';sc.display.shading.color_type='OBJECT';sc.display.shading.show_cavity=True
    sc.render.resolution_x=2400;sc.render.resolution_y=420
    cam=bpy.data.objects.new('cam',bpy.data.cameras.new('cam'));sc.collection.objects.link(cam);sc.camera=cam
    cam.data.type='ORTHO';cam.data.ortho_scale=x*1.02;cam.location=(x*.5,-300,90);cam.rotation_euler=(math.radians(73),0,0)
    for o in near:o.color=(.86,.83,.96,1)
    os.makedirs(SCRATCH,exist_ok=True);sc.render.filepath=os.path.join(SCRATCH,'cloud_kit_preview.png')
    bpy.ops.render.render(write_still=True)
    print('CLOUD preview ->',sc.render.filepath)

rng=np.random.default_rng(SEED);sea_rng=np.random.default_rng(SEED+1014)
def main():
    clear()
    obs=[]
    for name,spec in specs(rng).items():obs+=build(name,spec)
    export(obs)
    print('CLOUD kit ->',os.path.join(OUT,'CloudKit.fbx'),f'({len(obs)} meshes)')
    if PREVIEW:preview(obs)

main()
