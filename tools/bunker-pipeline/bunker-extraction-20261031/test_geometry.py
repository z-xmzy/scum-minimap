import unittest
from bunker_geometry import ROOT, placements
import numpy as np
from floorplans import transform_vertices, transform_triangles, cut_segments, ray_heights


class GeometryTests(unittest.TestCase):
    def test_unreal_axis_rotations(self):
        identity={'location':[0,0,0],'rotation':[0,0,0],'scale':[1,1,1]}
        for rotation,vector,expected in [([0,90,0],[1,0,0],[0,1,0]),([90,0,0],[1,0,0],[0,0,1]),([0,0,90],[0,1,0],[0,0,-1])]:
            t=dict(identity,rotation=rotation)
            np.testing.assert_allclose(transform_vertices(np.array([vector]),t),[expected],atol=1e-8)
        t=dict(identity,location=[20,30,40],scale=[2,3,4])
        np.testing.assert_allclose(transform_vertices(np.array([[1,1,1]]),t),[[22,33,44]])

    def test_mirrored_placement_keeps_floor_facing_up(self):
        # Counter-clockwise from above: an upward floor triangle.
        vertices=np.array([[0,0,0],[100,0,0],[0,100,0]],dtype=float);faces=np.array([[0,1,2]])
        for scale in ([1,1,1],[1,-1,1],[-1,1,1],[-1,-1,1]):
            tri=transform_triangles(vertices,faces,{'location':[0,0,0],'rotation':[0,0,0],'scale':scale})[0]
            self.assertGreater(np.cross(tri[1]-tri[0],tri[2]-tri[0])[2],0,scale)

    def test_cut_does_not_connect_separate_floors(self):
        triangles=np.array([[[0,0,0],[10,0,0],[0,0,10]],[[0,0,20],[10,0,20],[0,0,30]]],dtype=float)
        segments=cut_segments(triangles,5)
        self.assertEqual(segments.shape,(1,2,2))
        self.assertAlmostEqual(np.linalg.norm(segments[0,0]-segments[0,1]),5)
        self.assertEqual(len(cut_segments(triangles,15)),0)

    def test_component_instances_are_not_double_counted(self):
        t={'location':[1,2,3],'rotation':[0,0,0],'scale':[1,1,1]}
        scene={'name':'test','actors':[{'name':'actor','mesh':'representative','components':[{'name':'mesh','mesh':'real','world':t,'instanceCount':1}], 'instances':[{'component':'mesh','world':t}]}]}
        result=list(placements(scene))
        self.assertEqual(len(result),1)
        self.assertEqual(result[0]['mesh'],'real')
        scene['actors'][0]['components'][0]['instanceCount']=2
        with self.assertRaises(AssertionError): list(placements(scene))

    def test_vertical_ray_keeps_stacked_floors_separate(self):
        triangles=np.array([[[0,0,0],[10,0,0],[0,10,0]],[[0,0,400],[10,0,400],[0,10,400]]],dtype=float)
        np.testing.assert_allclose(ray_heights(triangles,2,2),[0,400])
        self.assertEqual(len(ray_heights(triangles,20,20)),0)


if __name__=='__main__': unittest.main()
