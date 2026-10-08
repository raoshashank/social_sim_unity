// Copyright (c) 2021, Members of Yale Interactive Machines Group, Yale University,
// Nathan Tsoi
// All rights reserved.
// This source code is licensed under the BSD-style license found in the
// LICENSE file in the root directory of this source tree. 

using System.Collections.Generic;
using UnityEngine;

namespace SEAN.Scenario.Agents
{
    public class Handcrafted : BaseAgentManager
    {
        public float SPAWN_HEIGHT = 0;
        public float WAYPOINT_DIST = 1.5f;

        public List<IVI.INavigable> agents;
        public List<Trajectory.TrackedGroup> groups;

        public GameObject agentPrefab;

        public int numWalker1Waypoints = 2;

        private PedestrianBehavior.SocialSituation current = PedestrianBehavior.SocialSituation.Empty;
        private GameObject agentsGO;
        private List<Pose> spawnLocations;
        private Dictionary<IVI.INavigable, List<Pose>> agentGoals = new Dictionary<IVI.INavigable, List<Pose>>();

        public Pose openGroupLocation = Pose.identity;

        public string scenario_name
        {
            get
            {
                return "Handcrafted_" + current;
            }
        }

        protected override void Awake()
        {
            base.Awake();
            agentGoals = new Dictionary<IVI.INavigable, List<Pose>>();
        }

        void Update()
        {
            if (current == PedestrianBehavior.SocialSituation.DownPath || current == PedestrianBehavior.SocialSituation.CrossPath || current == PedestrianBehavior.SocialSituation.CustomScenario)
            {
                foreach (var agent in agents)
                {
                    if (agent == null) { continue; }
                    if (agentGoals.ContainsKey(agent) && agentGoals[agent].Count > 1)
                    {
                        Vector3 agentPos = agent.transform.position;
                        agentPos.y = 0;
                        Vector3 goalPos = agentGoals[agent][0].position;
                        goalPos.y = 0;
                        float dist = Vector3.Distance(agentPos, goalPos);
                        if (agent.CloseEnough() || dist <= WAYPOINT_DIST)
                        {
                            agent.InitDest(agentGoals[agent][1].position);
                            Pose currentGoal = agentGoals[agent][0];
                            agentGoals[agent].RemoveAt(0);
                            agentGoals[agent].Add(currentGoal);
                        }
                    }
                }
            }
        }

        public void NewScenario(PedestrianBehavior.SocialSituation situation, GameObject socialSituationEnv, GameObject spawnLocations)
        {
            current = situation;
            // Must clear before registering the new group
            Clear();
            //print("new scenario: " + current);
            this.spawnLocations = new List<Pose>();
            foreach (Transform s in spawnLocations.transform)
            {
                if (current == PedestrianBehavior.SocialSituation.JoinGroup || current == PedestrianBehavior.SocialSituation.LeaveGroup)
                {
                    groups.Add(Trajectory.TrackedGroup.GetOrAttach(s.gameObject));
                }
                // Put spawn positions on the ground plane
                Vector3 p = s.position;
                p.y = 0;
                this.spawnLocations.Add(new Pose(p, s.rotation));
            }
            // get agents game object
            foreach (Transform transform in socialSituationEnv.transform)
            {
                if (transform.gameObject.name == "Agents")
                {
                    agentsGO = transform.gameObject;
                }
            }
            SpawnAgents();
        }

        void Clear()
        {
            agents = new List<IVI.INavigable>();
            groups = new List<Trajectory.TrackedGroup>();
            agentGoals.Clear();
            openGroupLocation = Pose.identity;
            if (!agentsGO) { return; }
            foreach (Transform child in agentsGO.transform)
            {
                GameObject.Destroy(child.gameObject);
            }
        }

        IVI.INavigable SpawnAgent(string name, Pose pose)
        {
            var sfRandom = Instantiate(agentPrefab, Vector3.zero, Quaternion.identity);
            IVI.INavigable agent = sfRandom.GetComponentInChildren<IVI.INavigable>();
            agent.name = name;
            agent.transform.position = pose.position;
            agent.transform.rotation = pose.rotation;
            agent.transform.parent = agentsGO.transform;
            agents.Add(agent);
            return agent;
        }

        void SpawnFixedGroup(Pose groupCenter, int numMembers)
        {
            float radius = 0.8f; // The distance each person stands from the center (standard conversational space)
            float angleStep = 360f / numMembers;
            for (int i = 0; i < numMembers; i++)
            {
                // 1. Calculate where they stand on the circle
                float angleRad = (i * angleStep) * Mathf.Deg2Rad;
                Vector3 offset = new Vector3(Mathf.Sin(angleRad) * radius, 0, Mathf.Cos(angleRad) * radius);
                Vector3 memberPos = groupCenter.position + offset;

                // 2. Make them rotate to face the center of the group
                Vector3 faceCenter = (groupCenter.position - memberPos).normalized;
                Quaternion rotation = Quaternion.LookRotation(faceCenter);

                // 3. Spawn them!
                Pose pose = new Pose(memberPos, rotation);
                SpawnAgent("CustomGroupAgent_" + i, pose);
            }
        }
        void SpawnGroup(Pose groupCenter)
        {
            IVI.GroupDataLoader.GroupData group = IVI.GroupDataLoader.groupData[Random.Range(0, IVI.GroupDataLoader.groupData.Count)];
            int openGroupIdx = Random.Range(0, group.pos.Count);
            for (int i = 0; i < group.pos.Count; i++)
            {

                float angle = Mathf.Atan(group.pos[i].x / group.pos[i].z);
                if (group.pos[i].z > 0)
                {
                    angle += Mathf.PI;
                }
                Quaternion rotation = Quaternion.Euler(0, angle * Mathf.Rad2Deg, 0);
                Pose pose = new Pose(groupCenter.position + group.pos[i], rotation);
                if (i == openGroupIdx)
                {
                    openGroupLocation = pose;
                }
                else
                {
                    SpawnAgent("Agent_" + i, pose);
                }
            }
        }

        void SpawnAgents()
{
    if (current == PedestrianBehavior.SocialSituation.Empty)
    {
        return;
    }
    if (current == PedestrianBehavior.SocialSituation.CustomScenario)
    {
        List<Pose> dynamicWaypoints = new List<Pose>();
        for (int i = 0; i < spawnLocations.Count; i++)
        {
            if (i == 0) 
            {
                SpawnFixedGroup(spawnLocations[i],3);
            }
            else
            {
                dynamicWaypoints.Add(spawnLocations[i]);
            }
        }
        List<Pose> walker2WPs = dynamicWaypoints.Count > numWalker1Waypoints
            ? dynamicWaypoints.GetRange(numWalker1Waypoints, dynamicWaypoints.Count - numWalker1Waypoints)
            : new List<Pose>();
        if (dynamicWaypoints.Count > numWalker1Waypoints)
            dynamicWaypoints.RemoveRange(numWalker1Waypoints, dynamicWaypoints.Count - numWalker1Waypoints);

        if (dynamicWaypoints.Count > 1) // Ensure we have at least a start and an end point!
        {
            IVI.INavigable agent = SpawnAgent("Agent_Dynamic", dynamicWaypoints[0]);
            Pose startPose = dynamicWaypoints[0];
            dynamicWaypoints.RemoveAt(0);
            dynamicWaypoints.Add(startPose);
            agentGoals.Add(agent, dynamicWaypoints);
            agent.InitDest(dynamicWaypoints[0].position);
        }
        else if (dynamicWaypoints.Count == 1)
        {
            Debug.LogWarning("You only have 1 dynamic waypoint! Add another marker to the folder for the walker to target.");
        }

        if (walker2WPs.Count > 1)
        {
            IVI.INavigable agent2 = SpawnAgent("Agent_Dynamic2", walker2WPs[0]);
            Pose startPose2 = walker2WPs[0];
            walker2WPs.RemoveAt(0);
            walker2WPs.Add(startPose2);
            agentGoals.Add(agent2, walker2WPs);
            agent2.InitDest(walker2WPs[0].position);
        }
        return;
    }
    if (current == PedestrianBehavior.SocialSituation.CustomScenario)
    {
        List<Pose> dynamicWaypoints = new List<Pose>();
        for (int i = 0; i < spawnLocations.Count; i++)
        {
            if (i == 0) 
            {
                SpawnFixedGroup(spawnLocations[i], 3);
            }
            else
            {
                dynamicWaypoints.Add(spawnLocations[i]);
            }
        }
        if (dynamicWaypoints.Count > 0)
        {
            List<Pose> trajectoryPoints = new List<Pose>(dynamicWaypoints);
            IVI.INavigable agent = SpawnAgent("Agent_Dynamic", trajectoryPoints[0]);
            agentGoals.Add(agent, trajectoryPoints);
            agent.InitDest(trajectoryPoints[0].position);
        }
        return;
    }
}
    }
}