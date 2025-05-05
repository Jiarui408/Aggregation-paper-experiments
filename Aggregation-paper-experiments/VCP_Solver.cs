using Gurobi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aggregation_paper_experiments
{
	class VCP_Solver
	{
		/// <summary>
		/// GUROBI聚合求解
		/// </summary>
		/// <param name="ds"></param>
		public void Gurobi_aggregate_solve(VCP_DataStructure ds)
		{
			GRBEnv env = new GRBEnv("Aggregate_VCP.log");
			int TimeLimit = ds.time;
			double OptimalGapLimit = ds.gap;
			env.Set(GRB.DoubleParam.MIPGap, OptimalGapLimit);//mipgap是可行解与目标之间的距离
			env.Set(GRB.DoubleParam.TimeLimit, TimeLimit);
			GRBModel Model = new GRBModel(env);

			Dictionary<string, GRBVar> Xak = new Dictionary<string, GRBVar>();
			Dictionary<string, GRBVar> Ya = new Dictionary<string, GRBVar>();
			Dictionary<string, GRBLinExpr> demand_node_balance = new Dictionary<string, GRBLinExpr>();
			Dictionary<string, GRBLinExpr> arc_capacity = new Dictionary<string, GRBLinExpr>();

			BuildGRBModel();
			Model.Optimize();
			int status = Model.Get(GRB.IntAttr.Status);
			int solution = Model.Get(GRB.IntAttr.SolCount);

			//可行则结果细化
			if (status == GRB.Status.OPTIMAL || (status == GRB.Status.TIME_LIMIT))
			{
				ds.aggregation_solving_time = Model.Runtime;
				ds.aggregation_gap = 0;
				ds.rollingstock_quantity = Convert.ToInt32(Model.ObjVal);
				foreach (var v in Xak)
				{
					if (v.Value.X > 0.0)
					{
						string arcname = v.Key;
						if (ds.arcs[arcname].mark != "virtualcon")
							ds.arc_quantity[arcname] = Convert.ToInt32(v.Value.X);
					}
				}

				Model.Dispose();
				env.Dispose();
			}
			//不可行
			else
			{
				Model.ComputeIIS();
				foreach (GRBConstr c in Model.GetConstrs())
				{
					if (Convert.ToBoolean(c.IISConstr))
						Console.WriteLine(c.ConstrName);
				}
				Model.Dispose();
				env.Dispose();
				Console.WriteLine("出现问题！");
			}

			/// <summary>
			/// 搭建模型
			/// </summary>
			void BuildGRBModel()
			{
				//决策变量
				BuildDecisionVar();
				//约束条件
				BuildConstraint();
			}
			/// <summary>
			/// 构建决策变量及目标函数
			/// </summary>
			void BuildDecisionVar()
			{
				//货流变量
				foreach (Arc a in ds.arcs.Values)
				{
					string s = a.name;
					if (a.mark == "virtualstart")
						Xak.Add(s, Model.AddVar(0.0, ds.rollingstocks.Count(), 1, GRB.CONTINUOUS, s));
					if (a.mark == "demand")
						Xak.Add(s, Model.AddVar(1.0, 1, 0, GRB.CONTINUOUS, s));
					if (a.mark != "virtualstart" && a.mark != "demand")
						Xak.Add(s, Model.AddVar(0.0, ds.rollingstocks.Count(), 0, GRB.CONTINUOUS, s));
					PrepareConstraintForAggregate(a, Xak[s], "aggregate");
				}
				Model.Set(GRB.IntAttr.ModelSense, 1);
			}

			/// <summary>
			/// 构建约束条件
			/// </summary>
			void BuildConstraint()
			{
				//约束1：流平衡约束
				BuildBalanceFlowConstraint();
			}

			/// <summary>
			/// 建立流平衡约束
			/// </summary>
			void BuildBalanceFlowConstraint()
			{
				//货流平衡			
				foreach (var v in demand_node_balance)
				{
					if (v.Key == "start")
					{
						Model.AddConstr(v.Value == -ds.rollingstocks.Count(), v.Key);
						continue;
					}
					if (v.Key == "end")
					{
						Model.AddConstr(v.Value == ds.rollingstocks.Count(), v.Key);
						continue;
					}
					if (v.Key != "end" && v.Key != "start")
					{
						Model.AddConstr(v.Value == 0, v.Key);
						continue;
					}
				}
			}

			void PrepareConstraintForAggregate(Arc a, GRBVar var, string GRBVar_name)
			{
				if (GRBVar_name == "aggregate")
				{
					if (demand_node_balance.ContainsKey(a.outnode.name) == false)
						demand_node_balance[a.outnode.name] = new GRBLinExpr();
					if (demand_node_balance.ContainsKey(a.innode.name) == false)
						demand_node_balance[a.innode.name] = new GRBLinExpr();
					demand_node_balance[a.outnode.name] = demand_node_balance[a.outnode.name] - var;
					demand_node_balance[a.innode.name] = demand_node_balance[a.innode.name] + var;
				}
			}
		}

		/// <summary>
		/// GUROBI解聚
		/// </summary>
		/// <param name="ds"></param>
		public void Gurobi_disaggregate_solve(VCP_DataStructure ds)
		{
			for (int i = 1; i <= ds.rollingstock_quantity; i++)
			{
				gurobi_solve_each_flow(i);
			}

			void gurobi_solve_each_flow(int i)
			{
				GRBEnv env = new GRBEnv("Disaggregate_VCP.log");
				int TimeLimit = ds.time;
				double OptimalGapLimit = ds.gap;
				env.Set(GRB.DoubleParam.MIPGap, OptimalGapLimit);//mipgap是可行解与目标之间的距离
				env.Set(GRB.DoubleParam.TimeLimit, TimeLimit);
				GRBModel Model = new GRBModel(env);

				Dictionary<string, GRBVar> Xak = new Dictionary<string, GRBVar>();
				Dictionary<int, Dictionary<string, GRBLinExpr>> demand_node_balance = new Dictionary<int, Dictionary<string, GRBLinExpr>>();
				Dictionary<string, GRBLinExpr> arc_capacity = new Dictionary<string, GRBLinExpr>();

				BuildGRBModel();
				Model.Optimize();
				int status = Model.Get(GRB.IntAttr.Status);
				int solution = Model.Get(GRB.IntAttr.SolCount);

				//可行则结果细化
				if (status == GRB.Status.OPTIMAL || (status == GRB.Status.TIME_LIMIT))
				{
					ds.disaggregation_solving_time = ds.disaggregation_solving_time + Model.Runtime;
					foreach (var v in Xak)
					{
						if (v.Value.X > 0.0)
						{
							string[] s = v.Key.Split('*');
							string arcname = s[1].ToString();
							ds.arc_quantity[arcname] = ds.arc_quantity[arcname] - Convert.ToInt32(v.Value.X);
						}
					}

					Model.Dispose();
					env.Dispose();
				}
				//不可行
				else
				{
					Model.ComputeIIS();
					foreach (GRBConstr c in Model.GetConstrs())
					{
						if (Convert.ToBoolean(c.IISConstr))
							Console.WriteLine(c.ConstrName);
					}
					Model.Dispose();
					env.Dispose();
					Console.WriteLine("出现问题！");
				}

				/// <summary>
				/// 搭建模型
				/// </summary>
				void BuildGRBModel()
				{
					//决策变量
					BuildDecisionVar();
					//约束条件
					BuildConstraint();
				}
				/// <summary>
				/// 构建决策变量及目标函数
				/// </summary>
				void BuildDecisionVar()
				{
					GRBLinExpr obj = new GRBLinExpr();
					//货流变量
					foreach (string aname in ds.arc_quantity.Keys)
					{
						if (ds.arc_quantity[aname] != 0)
						{
							Arc a = ds.arcs[aname];
							string s = i + "*" + a.name;
							Xak.Add(s, Model.AddVar(0.0, 1.0, 0, GRB.BINARY, s));
							PrepareConstraint(ds.rollingstocks[i], a, Xak[s], "Xak");
						}
					}
				}

				/// <summary>
				/// 构建约束条件
				/// </summary>
				void BuildConstraint()
				{
					//约束1：流平衡约束
					BuildBalanceFlowConstraint();
					//约束2：耦合约束
					BuildBundleConstraint();
				}

				/// <summary>
				/// 建立耦合约束
				/// </summary>
				void BuildBundleConstraint()
				{
					foreach (var v in arc_capacity)
					{
						if (ds.arc_quantity[v.Key] != 0)
							Model.AddConstr(v.Value <= ds.arc_quantity[v.Key], v.Key);
					}
				}

				/// <summary>
				/// 建立流平衡约束
				/// </summary>
				void BuildBalanceFlowConstraint()
				{
					//货流平衡、

					RollingStock rs = ds.rollingstocks[i];
					Node startnode = ds.nodes["start"];
					Node endnode = ds.nodes["end"];
					foreach (var v in demand_node_balance[rs.name])
					{
						if (v.Key == startnode.name)
						{
							Model.AddConstr(v.Value == -1, rs.name + "-" + v.Key);
							continue;
						}
						if (v.Key == endnode.name)
						{
							Model.AddConstr(v.Value == 1, rs.name + "-" + v.Key);
							continue;
						}
						if (v.Key != startnode.name && v.Key != endnode.name)
						{
							Model.AddConstr(v.Value == 0, rs.name + "-" + v.Key);
							continue;
						}
					}
				}


				void PrepareConstraint(RollingStock d, Arc a, GRBVar var, string GRBVar_name)
				{
					if (GRBVar_name == "Xak")
					{
						if (demand_node_balance.ContainsKey(d.name) == false)
							demand_node_balance[d.name] = new Dictionary<string, GRBLinExpr>();
						if (demand_node_balance[d.name].ContainsKey(a.outnode.name) == false)
							demand_node_balance[d.name][a.outnode.name] = new GRBLinExpr();
						if (demand_node_balance[d.name].ContainsKey(a.innode.name) == false)
							demand_node_balance[d.name][a.innode.name] = new GRBLinExpr();
						demand_node_balance[d.name][a.outnode.name] = demand_node_balance[d.name][a.outnode.name] - var;
						demand_node_balance[d.name][a.innode.name] = demand_node_balance[d.name][a.innode.name] + var;

						if (arc_capacity.ContainsKey(a.name) == false)
							arc_capacity[a.name] = new GRBLinExpr();
						arc_capacity[a.name] = arc_capacity[a.name] + var;
					}
				}
			}
		}

		/// <summary>
		/// GUROBI分着求解
		/// </summary>
		/// <param name="ds"></param>
		public void Gurobi_single_solve(VCP_DataStructure ds)
		{
			GRBEnv env = new GRBEnv("Original_VCP.log");
			int TimeLimit = ds.time;
			double OptimalGapLimit = ds.gap;
			env.Set(GRB.DoubleParam.MIPGap, OptimalGapLimit);//mipgap是可行解与目标之间的距离
			env.Set(GRB.DoubleParam.TimeLimit, TimeLimit);
			GRBModel Model = new GRBModel(env);

			Dictionary<string, GRBVar> Xak = new Dictionary<string, GRBVar>();
			Dictionary<string, GRBVar> Ya = new Dictionary<string, GRBVar>();
			Dictionary<int, Dictionary<string, GRBLinExpr>> demand_node_balance = new Dictionary<int, Dictionary<string, GRBLinExpr>>();
			Dictionary<string, GRBLinExpr> arc_capacity = new Dictionary<string, GRBLinExpr>();

			BuildGRBModel();
			Model.Optimize();
			int status = Model.Get(GRB.IntAttr.Status);
			int solution = Model.Get(GRB.IntAttr.SolCount);

			//可行则结果细化
			if (status == GRB.Status.OPTIMAL || (status == GRB.Status.TIME_LIMIT))
			{
				ds.original_solving_time = Model.Runtime;
				ds.original_gap = Model.MIPGap;
				Model.Dispose();
				env.Dispose();
			}
			//不可行
			else
			{
				Model.ComputeIIS();
				foreach (GRBConstr c in Model.GetConstrs())
				{
					if (Convert.ToBoolean(c.IISConstr))
						Console.WriteLine(c.ConstrName);
				}
				Model.Dispose();
				env.Dispose();
				Console.WriteLine("出现问题！");
			}

			/// <summary>
			/// 搭建模型
			/// </summary>
			void BuildGRBModel()
			{
				//决策变量
				BuildDecisionVar();
				//约束条件
				BuildConstraint();
			}
			/// <summary>
			/// 构建决策变量及目标函数
			/// </summary>
			void BuildDecisionVar()
			{
				GRBLinExpr obj = new GRBLinExpr();
				//货流变量
				foreach (Arc a in ds.arcs.Values)
				{
					foreach (RollingStock stock in ds.rollingstocks.Values)
					{
						string s = stock.name + "*" + a.name;
						if (a.mark == "virtualstart")
							Xak.Add(s, Model.AddVar(0.0, 1.0, 1, GRB.BINARY, s));
						if (a.mark != "virtualstart")
							Xak.Add(s, Model.AddVar(0.0, 1.0, 0, GRB.BINARY, s));
						PrepareConstraint(stock, a, Xak[s], "Xak");
					}
				}
				Model.Set(GRB.IntAttr.ModelSense, 1);
			}

			/// <summary>
			/// 构建约束条件
			/// </summary>
			void BuildConstraint()
			{
				//约束1：流平衡约束
				BuildBalanceFlowConstraint();
				//约束2：耦合约束
				BuildBundleConstraint();
			}

			/// <summary>
			/// 建立耦合约束
			/// </summary>
			void BuildBundleConstraint()
			{
				foreach (var v in arc_capacity)
				{
					Model.AddConstr(v.Value == 1, v.Key);
				}
			}

			/// <summary>
			/// 建立流平衡约束
			/// </summary>
			void BuildBalanceFlowConstraint()
			{
				//货流平衡
				foreach (RollingStock rs in ds.rollingstocks.Values)
				{
					Node startnode = ds.nodes["start"];
					Node endnode = ds.nodes["end"];
					foreach (var v in demand_node_balance[rs.name])
					{
						if (v.Key == startnode.name)
						{
							Model.AddConstr(v.Value == -1, rs.name + "-" + v.Key);
							continue;
						}
						if (v.Key == endnode.name)
						{
							Model.AddConstr(v.Value == 1, rs.name + "-" + v.Key);
							continue;
						}
						if (v.Key != startnode.name && v.Key != endnode.name)
						{
							Model.AddConstr(v.Value == 0, rs.name + "-" + v.Key);
							continue;
						}
					}
				}
			}


			void PrepareConstraint(RollingStock rs, Arc a, GRBVar var, string GRBVar_name)
			{
				if (GRBVar_name == "Xak")
				{
					if (demand_node_balance.ContainsKey(rs.name) == false)
						demand_node_balance[rs.name] = new Dictionary<string, GRBLinExpr>();
					if (demand_node_balance[rs.name].ContainsKey(a.outnode.name) == false)
						demand_node_balance[rs.name][a.outnode.name] = new GRBLinExpr();
					if (demand_node_balance[rs.name].ContainsKey(a.innode.name) == false)
						demand_node_balance[rs.name][a.innode.name] = new GRBLinExpr();
					demand_node_balance[rs.name][a.outnode.name] = demand_node_balance[rs.name][a.outnode.name] - var;
					demand_node_balance[rs.name][a.innode.name] = demand_node_balance[rs.name][a.innode.name] + var;

					if (a.mark == "demand")
					{
						if (arc_capacity.ContainsKey(a.name) == false)
							arc_capacity[a.name] = new GRBLinExpr();
						arc_capacity[a.name] = arc_capacity[a.name] + var;
					}
				}
			}
		}
	}
}
