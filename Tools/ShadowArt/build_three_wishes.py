"""Original level geometry, inspired by Mitra & Pauly's shadow-hull construction.
Intersects three orthographic silhouette extrusions at 0/+45/-45 degrees.
No images/meshes from the paper are reused. Each half-width profile is piecewise linear.
The 12 pieces are an exact partition of the feasible hull, not three overlaid pictures.
Run with local Python + numpy/scipy/Pillow. Output is baked JSON read by Unity.
"""
from pathlib import Path
from itertools import combinations
import json, math
import numpy as np
from scipy.spatial import ConvexHull
from PIL import Image, ImageDraw

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Assets/ShadowWorkshop/Resources/ShadowArt/ThreeWishes.json'
PROFILES=[
 [(0,.10),(.08,.10),(.085,.08),(.16,.08),(.165,.065),(.66,.045),(.665,.070),(.71,.070),(.715,.035),(.85,.035),(.855,.055),(.87,.055),(1,.004)],
 [(0,.080),(.06,.045),(.18,.045),(.185,.150),(.40,.065),(.405,.125),(.62,.045),(.625,.095),(.82,.025),(.825,.065),(1,.004)],
 [(0,.075),(.10,.11),(.20,.11),(.33,.065),(.72,.065),(.82,.050),(1,.004)]
]
AXES=np.array([[1.,0,0],[2**-.5,0,-2**-.5],[2**-.5,0,2**-.5]])
HOLES={0:[(.391,0),(.394,.014),(.420,.014),(.423,0)],
       2:[(.35+.024*math.sin(a),.024*math.cos(a)) for a in np.linspace(-math.pi/2,math.pi/2,17)]}
def width(v,t):
    p=np.array(PROFILES[v]);return np.interp(t,p[:,0],p[:,1])

def planes(y0,y1,quadrant):
    t0=(y0-.08)/.42;t1=(y1-.08)/.42
    result=[]
    for v,axis in enumerate(AXES):
        slope=(width(v,t1)-width(v,t0))/(y1-y0);base=width(v,t0)-slope*y0
        for sign in [-1,1]:
            normal=sign*axis.copy();normal[1]=-slope;result.append([*normal,base])
    result.extend([[0,-1,0,-y0],[0,1,0,y1]])
    if quadrant is not None:
        sx,sz=quadrant;result.extend([[-sx,0,0,0],[0,0,-sz,0]])
    return np.array(result)

def polyhedron(p):
    combos=np.array(list(combinations(range(len(p)),3)))
    a=p[combos,:3];b=p[combos,3]
    keep=np.abs(np.linalg.det(a))>1e-9
    vertices=np.linalg.solve(a[keep],b[keep,:,None]).squeeze(-1)
    vertices=vertices[np.all(vertices@p[:,:3].T<=p[:,3]+1e-8,axis=1)]
    vertices=np.unique(np.round(vertices,9),axis=0)
    if len(vertices)<4:return None
    h=ConvexHull(vertices)
    if h.volume<1e-12:return None
    faces=[]
    for tri,eq in zip(h.simplices,h.equations):
        tri=tri.copy();a,b,c=vertices[tri]
        if np.dot(np.cross(b-a,c-a),eq[:3])<0:tri=tri[[0,2,1]]
        faces.append(tri.tolist())
    return vertices,np.array(faces),h.volume

def carved_cells(y0,y1,quadrant):
    branches=[planes(y0,y1,quadrant)]
    for view,profile in HOLES.items():
        h=np.array(profile);a=np.interp(y0,h[:,0],h[:,1],left=0,right=0);b=np.interp(y1,h[:,0],h[:,1],left=0,right=0)
        if max(a,b)<1e-8:continue
        slope=(b-a)/(y1-y0);base=a-slope*y0;next_branches=[]
        for p in branches:
            for side in [-1,1]:
                normal=-side*AXES[view].copy();normal[1]=slope
                next_branches.append(np.vstack([p,[*normal,-base]]))
        branches=next_branches
    return [cell for p in branches if (cell:=polyhedron(p)) is not None]

def main():
    knots=sorted(set([.08+t*.42 for p in PROFILES for t,_ in p]+[.22,.36]+[y for profile in HOLES.values() for y,_ in profile]))
    pieces=[];total=0
    for band,(lo,hi) in enumerate(zip([.08,.22,.36],[.22,.36,.50])):
        for q,(sx,sz) in enumerate([(-1,-1),(1,-1),(-1,1),(1,1)]):
            cells=[];surface=[];volume=0
            for y0,y1 in zip(knots[:-1],knots[1:]):
                if y0<lo-1e-6 or y1>hi+1e-6:continue
                for v,t,vol in carved_cells(y0,y1,(sx,sz)):
                    cells.append((v,t));volume+=vol
                    for tri in t:
                        points=v[tri]
                        # Remove only the interior horizontal faces between adjacent profile slabs.
                        if np.ptp(points[:,1])<1e-7 and lo+1e-7<points[0,1]<hi-1e-7:continue
                        surface.extend(points)
            vertices=np.array(surface);center=(vertices.min(axis=0)+vertices.max(axis=0))*.5
            pieces.append(dict(name=f'{["FOUNDATION","HEART","CROWN"][band]} {q+1}',band=band,
                position=center.tolist(),vertices=(vertices-center).reshape(-1).round(7).tolist(),
                colliders=[dict(vertices=(v-center).reshape(-1).round(7).tolist(),triangles=t.reshape(-1).tolist()) for v,t in cells],volume=volume))
            total+=volume
    # Independently project the actual hull extrema to compare with each intended input.
    img=Image.new('RGB',(1200,620),'#111e28');d=ImageDraw.Draw(img)
    errors=[]
    for view,title in enumerate(['HOME / LIGHTHOUSE','GROWTH / PINE','JOURNEY / ROCKET']):
        d.text((view*400+65,15),title,fill='#f5cf8e')
        misses=area=0
        for row in range(500):
            y=.08+(499-row)/499*.42;t=(y-.08)/.42
            intervals=[(min(v@AXES[view]),max(v@AXES[view])) for v,_,_ in carved_cells(y-1e-6,y+1e-6,None)]
            desired=width(view,t);hole=0
            if view in HOLES:
                h=np.array(HOLES[view]);hole=np.interp(y,h[:,0],h[:,1],left=0,right=0)
            for px in range(380):
                u=(px-190)/1100;want=abs(u)<=desired and (hole<1e-8 or abs(u)>=hole)
                actual=any(a<=u<=b for a,b in intervals)
                area+=want;misses+=want and not actual
                if want or actual:d.point((view*400+px+10,row+55),fill='#c5ddd0' if actual else '#a25e46')
        errors.append(misses/area)
        d.text((view*400+60,578),f'feasible coverage {(1-errors[-1])*100:.2f}%',fill='white')
    path=ROOT/'Logs/ShadowArt/three-wishes-targets.png';path.parent.mkdir(parents=True,exist_ok=True);img.save(path)
    OUT.parent.mkdir(parents=True,exist_ok=True)
    OUT.write_text(json.dumps(dict(version=1,title='THREE WISHES',pieces=pieces,profiles=PROFILES),separators=(',',':')))
    print(json.dumps(dict(pieces=len(pieces),surfaceTriangles=sum(len(p['vertices'])//9 for p in pieces),convexCells=sum(len(p['colliders']) for p in pieces),coverage=[1-x for x in errors],volume=total,preview=str(path))))

if __name__=='__main__':main()
