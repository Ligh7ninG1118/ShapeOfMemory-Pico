"""Torch levels: a fixed cluster of everyday objects on a turntable, lit by a hand-held flashlight.
Only from one direction does the cluster's shadow on the wall become a picture related to the objects.
Left hand turns the turntable, right hand moves the flashlight (a point light). Mirrors TorchProjection.cs.

Design happens in picture space: each object gets a target point on the wall and a magnification
(shadow size / object size). Its 3D position is then placed on the ray from the solution light through that
point, so objects end up at very different depths and the cluster looks like a random pile from elsewhere.
Unity rasterises the imported meshes at the solution to build the target, so the answer is always exact.
Run: python Tools/ShadowArt/build_torch.py [--check]  (numpy + Pillow). Previews: Logs/ShadowArt/Torch-*.png
"""
from pathlib import Path
import json, math, sys
import numpy as np
from PIL import Image, ImageDraw
sys.path.insert(0,str(Path(__file__).resolve().parent))
from build_everyday import Mesh, rot, box, lathe, sphere, tube, extrude, rrect, arc

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Assets/ShadowWorkshop/Art/Torch/TorchObjects.json'
LOGS=ROOT/'Logs/ShadowArt'
# Workbench-local metres, same frame as the other shadow levels (table top at y=0).
WALL_Z,WALL_X0,WALL_Y0,PX,WW,WH=.60,-.72,0.,.009,160,120
AXIS=np.array([0.,0.,.05])              # turntable axis (vertical) through x=0,z=.05
CENTER=np.array([0.,.30,.05])           # cluster centre; the desktop light orbits it
HALF_ANGLE=32                           # flashlight cone half angle
N,R0,TOL=64,12,1                        # normalised compare grid, RMS radius in cells, tolerance in cells
# A 2-cell tolerance on both masks let a visibly broken picture pass (IoU .58), hence the IoU floor.
COVER_MIN,SPILL_MAX,IOU_MIN=.90,.10,.62

MATERIALS={
 'vinyl':(.06,.06,.07,.85),'record label':(.85,.20,.18,.30),'cd silver':(.82,.84,.88,.95),'cd print':(.30,.55,.85,.40),
 'mic grille':(.55,.57,.60,.75),'mic body':(.12,.12,.13,.50),'wood':(.78,.58,.36,.35),'harmonica':(.80,.82,.85,.90),
 'cassette':(.15,.16,.18,.40),'cassette label':(.95,.88,.70,.20),'pick':(.95,.55,.12,.60),
 'tennis':(.80,.92,.25,.20),'tennis seam':(.97,.97,.95,.20),'treat':(.62,.40,.22,.25),'sock':(.85,.30,.30,.10),'sock stripe':(.97,.97,.97,.10),
 'kibble bag':(.30,.55,.75,.35),'bag label':(.98,.85,.35,.30),'bowl steel':(.75,.77,.80,.90),'bone':(.95,.92,.84,.25),
 'rope':(.25,.45,.85,.10),'rope knot':(.92,.35,.30,.10),'dog tag':(.90,.75,.30,.85),
 'clock body':(.20,.42,.70,.50),'clock face':(.97,.96,.92,.30),'clock hands':(.10,.10,.10,.40),'cup':(.25,.27,.30,.45),
 'note yellow':(.98,.88,.35,.15),'eraser':(.95,.55,.60,.15),'pencil':(.98,.75,.15,.30),'pencil tip':(.90,.78,.60,.20),'graphite':(.15,.15,.15,.50),
 'pen':(.15,.30,.75,.60),'ruler':(.90,.88,.80,.40),'marker':(.20,.70,.40,.40),'cap':(.10,.10,.12,.50),
}

# ---------------------------------------------------------------- objects
def vinyl_record():
    m=Mesh('Vinyl single')
    m.add(lathe([(.0,0),(.072,0),(.072,.003),(.0,.003)],28),'vinyl')
    for y in(-.0004,.0034):m.add(lathe([(.0,y),(.024,y)] if y<0 else [(.024,y),(.0,y)],20,caps=False),'record label')
    return m.centred()
def compact_disc():
    m=Mesh('Compact disc')
    m.add(lathe([(.0,0),(.06,0),(.06,.0018),(.0,.0018)],28),'cd silver')
    m.add(lathe([(.058,.0022),(.020,.0022)],24,caps=False),'cd print')
    return m.centred()
def microphone():
    m=Mesh('Microphone')
    m.add(lathe([(.0,0),(.011,0),(.016,.115),(.019,.122),(.0,.122)],16),'mic body')
    m.add(sphere(.026,16,10),'mic grille',t=(0,.142,0))
    return m.centred()
def drumstick():
    m=Mesh('Drumstick')
    m.add(lathe([(.0,0),(.0075,0),(.0075,.13),(.0042,.205),(.0,.205)],10),'wood')
    m.add(sphere(.0065,10,6),'wood',t=(0,.207,0))
    return m.centred()
def harmonica():
    m=Mesh('Harmonica')
    m.add(box(.105,.026,.022),'harmonica');m.add(box(.098,.010,.024),'wood')
    return m.centred()
def cassette():
    m=Mesh('Cassette tape')
    m.add(box(.100,.064,.012),'cassette');m.add(box(.080,.030,.0125),'cassette label',t=(0,.010,0))
    return m.centred()
def guitar_pick():
    m=Mesh('Guitar pick')
    pts=arc(0,.010,.016,20,160,7)+[(0,-.022)]
    m.add(extrude(pts,.002),'pick')
    return m.centred()

def tennis_ball():
    m=Mesh('Tennis ball');m.add(sphere(.034,18,12),'tennis')
    m.add(tube([(.0345*math.cos(a),.010*math.sin(2*a),.0345*math.sin(a)) for a in np.linspace(0,2*math.pi,24)],.0025,6),'tennis seam')
    return m.centred()
def treat_stick():
    m=Mesh('Dental treat');m.add(extrude(rrect(.085,.024,.010,4),.022),'treat');return m.centred()
def sock():
    m=Mesh('Stolen sock')
    path=[(0,.075,0),(0,.020,0),(.004,.004,0),(.020,-.006,0),(.050,-.008,0)]
    m.add(tube(path,[.017,.018,.018,.017,.015],12),'sock')
    m.add(tube([(0,.078,0),(0,.066,0)],.0185,12),'sock stripe')
    return m.centred()
def kibble_bag():
    m=Mesh('Kibble bag')
    m.add(extrude([(-.055,-.085),(.055,-.085),(.058,.055),(.045,.085),(-.045,.085),(-.058,.055)],.060),'kibble bag')
    m.add(box(.07,.05,.062),'bag label',t=(0,.0,0))
    return m.centred()
def rawhide():
    m=Mesh('Rawhide stick')
    m.add(lathe([(.0,0),(.010,0),(.010,.14),(.0,.14)],10),'treat')
    for y in(.0,.14):m.add(sphere(.014,10,6),'treat',t=(0,y,0),scale=(1,.7,1))
    return m.centred()
def food_bowl():
    m=Mesh('Dog bowl')
    m.add(lathe([(.0,.0),(.082,.0),(.060,.045),(.054,.045),(.074,.006),(.0,.006)],24),'bowl steel')
    return m.centred()
def chew_bone():
    m=Mesh('Chew bone')
    m.add(lathe([(.0,-.06),(.011,-.06),(.011,.06),(.0,.06)],12),'bone')
    for y in(-.064,.064):
        for x in(-.012,.012):m.add(sphere(.016,12,8),'bone',t=(x,y,0))
    return m.centred()
def rope_toy():
    m=Mesh('Rope toy')
    path=[(.09*math.cos(a)-.09,.09*math.sin(a),0) for a in np.linspace(0,math.radians(75),10)]
    m.add(tube(path,.012,10),'rope')
    for p in(path[0],path[-1]):m.add(sphere(.020,12,8),'rope knot',t=p)
    return m.centred()
def dog_tag():
    m=Mesh('Dog tag');m.add(lathe([(.0,0),(.016,0),(.016,.002),(.0,.002)],16),'dog tag',e=(90,0,0));return m.centred()

def desk_clock():
    m=Mesh('Desk clock')
    m.add(extrude(arc(0,0,.074,0,360,29)[:-1],.034),'clock body')
    m.add(extrude(arc(0,0,.064,0,360,25)[:-1],.002),'clock face',t=(0,0,-.0175))
    for a,l in((90,.045),(20,.032)):m.add(box(l,.004,.003),'clock hands',e=(0,0,a),t=(.5*l*math.cos(math.radians(a)),.5*l*math.sin(math.radians(a)),-.019))
    m.add(box(.050,.030,.030),'clock body',t=(0,-.078,0))
    return m.centred()
def pencil_cup():
    m=Mesh('Pencil cup');m.add(lathe([(.0,0),(.036,0),(.040,.095),(.036,.095),(.033,.006),(.0,.006)],18),'cup');return m.centred()
def sticky_notes():
    m=Mesh('Sticky notes');m.add(box(.078,.018,.078),'note yellow');return m.centred()
def eraser():
    m=Mesh('Eraser');m.add(box(.052,.018,.026),'eraser');return m.centred()
def pencil():
    m=Mesh('Pencil stub')
    m.add(lathe([(.0,0),(.0045,0),(.0045,.095)],6),'pencil')
    m.add(lathe([(.0045,.095),(.0012,.115),(.0,.119)],6,caps=False),'pencil tip')
    m.add(lathe([(.0,-.012),(.0047,-.012),(.0047,.0)],6),'eraser')
    return m.centred()
def pen():
    m=Mesh('Ballpoint pen')
    m.add(lathe([(.0,0),(.0055,0),(.0055,.125),(.002,.140),(.0,.142)],10),'pen')
    m.add(box(.003,.050,.004),'cap',t=(.0065,.10,0))
    return m.centred()
def ruler():
    m=Mesh('Ruler');m.add(box(.030,.200,.003),'ruler');return m.centred()
def marker():
    m=Mesh('Highlighter')
    m.add(lathe([(.0,0),(.009,0),(.009,.090),(.0,.090)],10),'marker');m.add(lathe([(.0,.088),(.0095,.088),(.0095,.125),(.0,.125)],10),'cap')
    return m.centred()

# ---------------------------------------------------------------- design
def euler_of(R):
    # Unity Euler (x,y,z) for R = Ry*Rx*Rz.
    a=math.asin(max(-1,min(1,-R[1][2])));b=math.atan2(R[0][2],R[2][2]);c=math.atan2(R[1][0],R[1][1])
    e=[round(math.degrees(x),3) for x in(a,b,c)];assert np.allclose(rot(e),R,atol=1e-4);return e
def D(u,v,mag,*turns):
    # Picture-space placement: shadow centre offset (u,v) from the cluster centre's shadow, magnification
    # (shadow size / object size), then world-axis turns applied in order, e.g. ('x',-90),('z',-20).
    R=np.eye(3)
    for axis,deg in turns:R=rot({'x':(deg,0,0),'y':(0,deg,0),'z':(0,0,deg)}[axis])@R
    return {'u':u,'v':v,'mag':mag,'euler':euler_of(R)}

LEVELS=[
 {'id':'Encore','title':'E N C O R E','button':'ENCORE','riddle':'A record, a disc, a microphone, a stick and a harmonica. Find the light that lets them sing.',
  'hint':'Light from the right and a little below. The two discs become the notes; the microphone and drumstick hold them up.',
  'complete':'ENCORE\nTwo notes, played by everything on the shelf.','accent':[.95,.62,.30],
  'light':[.10,.22,-.40],'solution_yaw':0,'start_yaw':60,
  'pieces':[
   (vinyl_record,'RECORD',D(-.17,-.24,1.8,('x',-90),('x',45),('z',-20))),
   (compact_disc,'CD',D(.11,-.17,2.1,('x',-90),('x',45),('z',-20))),
   (microphone,'MICROPHONE',D(-.07,.02,2.9,('z',3))),
   (drumstick,'DRUMSTICK',D(.21,.10,2.4,('z',-2))),
   (harmonica,'HARMONICA',D(.07,.31,2.75,('z',16))),
   (cassette,'CASSETTE',D(-.17,-.24,2.2,('y',50),('z',-20))),
   (guitar_pick,'PICK',D(.11,-.17,2.6,('x',-90),('z',10))),
  ]},
 {'id':'GoodBoy','title':'G O O D   B O Y','button':'GOOD BOY','riddle':'Kibble, a ball, a treat, a sock, two chews, a rope and a bowl. Someone is waiting by the door.',
  'hint':'Keep the light low and in front. The bag is the body, the ball the head, two chews are legs and the rope wags.',
  'complete':'GOOD BOY\nEverything he owns, and the one thing he stole.','accent':[.55,.80,.95],
  'light':[-.02,.16,-.42],'solution_yaw':0,'start_yaw':75,
  'pieces':[
   (kibble_bag,'KIBBLE',D(.02,.02,2.0,('z',90))),
   (tennis_ball,'TENNIS BALL',D(-.19,.17,2.3)),
   (treat_stick,'TREAT',D(-.285,.135,2.0,('z',12))),
   (sock,'SOCK',D(-.145,.15,1.8,('z',-10))),
   (chew_bone,'BONE',D(-.10,-.21,1.9)),
   (rawhide,'RAWHIDE',D(.14,-.21,2.2,('z',-4))),
   (rope_toy,'ROPE',D(.235,.12,1.9,('z',-25))),
   (food_bowl,'BOWL',D(-.31,-.335,1.9)),
   (dog_tag,'TAG',D(-.19,.17,2.9,('y',30))),
  ]},
 {'id':'BrightIdea','title':'B R I G H T   I D E A','button':'BRIGHT IDEA','riddle':'A clock, a pencil cup, sticky notes, an eraser and three things to write with. It is late, and something is coming.',
  'hint':'Hold the light low, below the pile. The clock is round; the cup and the notes make the base; the pens shine.',
  'complete':'BRIGHT IDEA\nThe desk at midnight, just before it clicks.','accent':[.98,.85,.35],
  'light':[.14,.12,-.36],'solution_yaw':0,'start_yaw':60,
  'pieces':[
   (desk_clock,'CLOCK',D(0,.08,2.3)),
   (pencil_cup,'PENCIL CUP',D(0,-.13,2.0)),
   (sticky_notes,'STICKY NOTES',D(0,-.235,1.9,('x',-8))),
   (eraser,'ERASER',D(0,-.295,2.4)),
   (pencil,'PENCIL',D(-.28,.10,2.3,('z',90))),
   (pen,'PEN',D(0,.36,2.3)),
   (marker,'HIGHLIGHTER',D(.235,.315,2.1,('z',-45))),
  ]},
]

# ---------------------------------------------------------------- projection (mirrors TorchProjection.cs)
def resolve(level):
    # Turn picture-space placements into cluster-local poses (designed at turntable yaw = solution_yaw).
    L=np.array(level['light'],float);undo=rot((0,-level['solution_yaw'],0));c=wall_point(CENTER,L)
    for make,label,d in level['pieces']:
        q=np.array([c[0]+d['u'],c[1]+d['v'],WALL_Z]);p=L+(q-L)/d['mag']
        d['position']=((p-AXIS)@undo.T+AXIS).round(4).tolist();d['local_euler']=euler_of(undo@rot(d['euler']))
    return L

def wall_point(p,L):
    t=(WALL_Z-L[2])/(p[2]-L[2]);return L+(np.asarray(p)-L)*t

def world(pieces,yaw):
    out=[];R=rot((0,yaw,0))
    for (v,tri),pose in pieces:
        w=(v@rot(pose['local_euler']).T+np.array(pose['position'])-AXIS)@R.T+AXIS;out.append((w,tri))
    return out

def shadow(pieces,yaw,L,aim=None,res=1):
    L=np.asarray(L,float);aim=CENTER-L if aim is None else np.asarray(aim,float);aim=aim/np.linalg.norm(aim)
    img=Image.new('L',(WW*res,WH*res),0);draw=ImageDraw.Draw(img)
    for w,tri in world(pieces,yaw):
        a,b,c=w[tri[:,0]],w[tri[:,1]],w[tri[:,2]];n=np.cross(b-a,c-a)
        facing=np.einsum('ij,ij->i',n,L-a)>0
        dz=w[:,2]-L[2];ok=dz>1e-3;t=(WALL_Z-L[2])/np.where(ok,dz,1);q=L+(w-L)*t[:,None]
        x=(q[:,0]-WALL_X0)/PX*res;y=(q[:,1]-WALL_Y0)/PX*res
        for k in np.nonzero(facing)[0]:
            i,j,l=tri[k]
            if ok[i] and ok[j] and ok[l]:draw.polygon([(x[i],WH*res-y[i]),(x[j],WH*res-y[j]),(x[l],WH*res-y[l])],fill=1)
    mask=np.array(img)[::-1].astype(bool)
    # Only lit wall shows a shadow: the flashlight cone.
    gx,gy=np.meshgrid((np.arange(WW*res)+.5)/res*PX+WALL_X0,(np.arange(WH*res)+.5)/res*PX+WALL_Y0)
    d=np.stack([gx-L[0],gy-L[1],np.full_like(gx,WALL_Z-L[2])],-1);d/=np.linalg.norm(d,axis=-1,keepdims=True)
    lit=(d@aim)>math.cos(math.radians(HALF_ANGLE))
    return mask&lit,lit

def normalise(mask):
    ys,xs=np.nonzero(mask);out=np.zeros((N,N),bool)
    if len(xs)<12:return out
    cx,cy=xs.mean()+.5,ys.mean()+.5;r=math.sqrt(((xs+.5-cx)**2+(ys+.5-cy)**2).mean())
    acc=np.zeros((N,N),int)
    for ox in(-.25,.25):
        for oy in(-.25,.25):
            X=np.floor(cx+(np.arange(N)+.5+ox-N/2)*r/R0).astype(int);Y=np.floor(cy+(np.arange(N)+.5+oy-N/2)*r/R0).astype(int)
            vx=(X>=0)&(X<mask.shape[1]);vy=(Y>=0)&(Y<mask.shape[0])
            acc[np.ix_(vy,vx)]+=mask[np.ix_(Y[vy],X[vx])]
    return acc>=2

def dilate(mask,r):
    out=mask.copy();h,w=mask.shape
    for dy in range(-r,r+1):
        for dx in range(-r,r+1):
            if dx*dx+dy*dy>r*r+r:continue
            s=np.zeros_like(mask);s[max(dy,0):h+min(dy,0),max(dx,0):w+min(dx,0)]=mask[max(-dy,0):h+min(-dy,0),max(-dx,0):w+min(-dx,0)];out|=s
    return out

def score(target,current):
    area=max(1,target.sum());cover=(target&dilate(current,TOL)).sum()/area;spill=(current&~dilate(target,TOL)).sum()/area
    iou=(target&current).sum()/max(1,(target|current).sum())
    return cover,spill,iou,cover>=COVER_MIN and spill<=SPILL_MAX and iou>=IOU_MIN

def orbit(L,dyaw=0,delev=0,scale=1):
    # Move the light on a sphere around the cluster centre (desktop control model).
    v=np.asarray(L,float)-CENTER;r=np.linalg.norm(v)*scale;az=math.atan2(v[0],-v[2])+math.radians(dyaw);el=math.asin(v[1]/np.linalg.norm(v))+math.radians(delev)
    return CENTER+r*np.array([math.sin(az)*math.cos(el),math.sin(el),-math.cos(az)*math.cos(el)])

def to_img(mask,scale=3,fg=(30,36,40),bg=(238,226,198)):
    img=Image.new('RGB',(mask.shape[1]*scale,mask.shape[0]*scale),bg);px=img.load()
    a=np.kron(mask[::-1],np.ones((scale,scale),bool));ys,xs=np.nonzero(a)
    arr=np.array(img);arr[ys,xs]=fg;return Image.fromarray(arr)

def top_view(pieces,yaw,L,scale=900):
    # Overhead check that the pile fits the turntable and spreads in depth.
    img=Image.new('RGB',(480,480),(40,44,48));d=ImageDraw.Draw(img);o=np.array([240,240])
    def P(p):return (o[0]+(p[0])*scale,o[1]-(p[2]-.05)*scale)
    d.ellipse([P((-.17,0,.22)),P((.17,0,-.12))],outline=(120,120,120))
    for w,tri in world(pieces,yaw):
        for i,j,k in tri[::3]:d.polygon([P(w[i]),P(w[j]),P(w[k])],fill=(200,170,120))
    d.ellipse([P(L)[0]-5,P(L)[1]-5,P(L)[0]+5,P(L)[1]+5],fill=(255,240,120))
    return img

def build(check=False):
    LOGS.mkdir(parents=True,exist_ok=True);rng=np.random.default_rng(5)
    data={'version':1,'wall':{'z':WALL_Z,'x0':WALL_X0,'y0':WALL_Y0,'px':PX,'width':WW,'height':WH},'axis':AXIS.tolist(),'center':CENTER.tolist(),
          'halfAngle':HALF_ANGLE,'compare':{'size':N,'radius':R0,'tolerance':TOL,'cover':COVER_MIN,'spill':SPILL_MAX,'iou':IOU_MIN},
          'materials':[{'name':k,'color':list(c[:3]),'smoothness':c[3]} for k,c in MATERIALS.items()],'objects':[],'levels':[]}
    for level in LEVELS:
        L=resolve(level);meshes=[];entry={k:level[k] for k in('id','title','button','riddle','hint','complete','accent','light','solution_yaw','start_yaw')};entry['pieces']=[]
        for make,label,d in level['pieces']:
            m=make();meshes.append((m.arrays(),{'position':d['position'],'local_euler':d['local_euler']}))
            data['objects'].append({'name':m.name,'vertices':[round(c,5) for c in np.array(m.v).flatten().tolist()],
                'submeshes':[{'material':k,'triangles':[i for t in ts for i in t]} for k,ts in m.sub.items()]})
            entry['pieces'].append({'object':m.name,'label':label,'position':d['position'],'euler':d['local_euler']})
            r=math.hypot(d['position'][0]-AXIS[0],d['position'][2]-AXIS[2])
            if r>.19 or not .14<=d['position'][1]<=.50:print(f"  ! {label} outside the turntable volume: r={r:.3f} y={d['position'][1]:.3f}")
        data['levels'].append(entry)
        sol,_=shadow(meshes,level['solution_yaw'],L);target=normalise(sol)
        # Robustness of the answer and of the search.
        yaws=[(dy,score(target,normalise(shadow(meshes,level['solution_yaw']+dy,L)[0]))) for dy in range(-30,31,3)]
        elev=[(de,score(target,normalise(shadow(meshes,level['solution_yaw'],orbit(L,0,de))[0]))) for de in range(-24,25,4)]
        dist=[(s,score(target,normalise(shadow(meshes,level['solution_yaw'],orbit(L,0,0,s))[0]))) for s in(.7,.8,.9,1.1,1.25,1.4)]
        rand=[]
        for _ in range(60):
            dy=rng.uniform(25,335);de=rng.uniform(-20,30);s=rng.uniform(.75,1.3)
            rand.append(score(target,normalise(shadow(meshes,level['solution_yaw']+dy,orbit(L,rng.uniform(-25,25),de,s))[0])))
        print(f"{level['id']}: yaw pass {[dy for dy,s in yaws if s[3]]}  elev pass {[de for de,s in elev if s[3]]}  dist pass {[s for s,x in dist if x[3]]}")
        print(f"   random views: pass {sum(s[3] for s in rand)}/60  iou median {np.median([s[2] for s in rand]):.2f} max {max(s[2] for s in rand):.2f}  cover max {max(s[0] for s in rand):.2f}")
        # Sheet: solution shadow | overhead pile | normalised target ; three other turntable angles below.
        sheet=Image.new('RGB',(WW*3*3+20,WH*3*2+10),(20,20,20))
        sheet.paste(to_img(sol),(0,0));sheet.paste(top_view(meshes,level['solution_yaw'],L).resize((WH*3,WH*3)),(WW*3+10,0))
        sheet.paste(to_img(target,5).resize((WH*3,WH*3)),(WW*3+20+WH*3,0))
        for k,dy in enumerate((40,120,220)):sheet.paste(to_img(shadow(meshes,level['solution_yaw']+dy,L)[0]),(k*(WW*3+10),WH*3+10))
        sheet.save(LOGS/f"Torch-{level['id']}.png")
    if not check:
        OUT.parent.mkdir(parents=True,exist_ok=True);OUT.write_text(json.dumps(data,separators=(',',':')))
        print('wrote',OUT.relative_to(ROOT),f'{OUT.stat().st_size/1024:.0f} KB')

if __name__=='__main__':build('--check' in sys.argv)
