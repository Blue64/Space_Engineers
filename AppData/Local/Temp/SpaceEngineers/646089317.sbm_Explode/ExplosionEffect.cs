using System.Collections.Generic;
using VRageMath;
using VRage.Utils;
using VRage.Game;
using Sandbox.Game.Entities;
using System;

namespace JumpExplode
{
	public class ExplosionEffect
	{
		private struct PointSpread
		{
			public Vector3D normal;
			public Vector3D pos;
			public PointSpread(Vector3D n1, Vector3D p1)
			{
				normal = n1;
				pos = p1;
			}
		}
		private Vector3D m_center;
		private bool m_done;
		private int count = 0;
		private List<PointSpread> points = new List<PointSpread>();
		MyEntity3DSoundEmitter emitter;
		MyEntity3DSoundEmitter cena;
		public bool done
		{
			get
			{
				return m_done;
			}
			private set
			{
				m_done = value;
			}
		}
		public ExplosionEffect(Vector3D center)
		{
			m_center = center;
			for(int cnt = 0; cnt < 500; cnt++)
			{
				var norm = MyUtils.GetRandomVector3Normalized();
                PointSpread point = new PointSpread(norm, center + Vector3D.Multiply(norm, MyUtils.GetRandomFloat(10, 50)));
				points.Add(point);
			}
		}



		internal void Play()
		{

			for (int cnt = 0; cnt < 50; cnt++)
			{
				var norm = MyUtils.GetRandomVector3Normalized();
				PointSpread point = new PointSpread(norm, m_center + Vector3D.Multiply(norm, MyUtils.GetRandomFloat(10, 50)));
				points.Add(point);
			}
			count++;
			if(count < 120)
			{
				foreach (var point in points)
				{
					var color = Color.LightBlue.ToVector4();
					Vector3D vec = Vector3D.Lerp(point.pos, m_center, count / 120d);
					var cnter = Vector3D.Lerp(vec, m_center, 0.2);

					MySimpleObjectDraw.DrawLine(vec, cnter, MyStringId.GetOrCompute("particle_laser"), ref color, 0.1f);
				}
				if (count == 42 && CoreWarpExplode.instance.isSpecial)
				{
					cena = new MyEntity3DSoundEmitter(null);
					cena.SetPosition(m_center);
					cena.SetVelocity(Vector3.Zero);
					MySoundPair m_bombExpl = new MySoundPair("ArcWepLrgCENAExpl");
					cena.CustomMaxDistance = (float)Math.Pow(50, 2);
					cena.CustomVolume = 15f;
					cena.PlaySingleSound(m_bombExpl, true);
				}
			}
			else if(count < 150)
			{
				if(count == 120)
				{
					emitter = new MyEntity3DSoundEmitter(null);
					emitter.SetPosition(m_center);
					emitter.SetVelocity(Vector3.Zero);
					MySoundPair m_bombExpl = new MySoundPair("ArcWepLrgWarheadExpl");
					emitter.CustomMaxDistance = (float)Math.Pow(50, 2);
					emitter.CustomVolume = 15f;
					emitter.PlaySingleSound(m_bombExpl, true);
				}

				foreach (var point in points)
				{
					var color = Color.LightBlue.ToVector4();
					Vector3D vec = Vector3D.Lerp(point.pos, m_center, (120 - count * 4d) / 120d);
					var cnter = Vector3D.Lerp(vec, m_center, 0.2);

					MySimpleObjectDraw.DrawLine(vec, cnter, MyStringId.GetOrCompute("particle_laser"), ref color, 0.1f);
				}
			}
			else
			{
				done = true;
			}
		}
	}
}
