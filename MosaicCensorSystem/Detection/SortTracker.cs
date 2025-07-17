using System;
using System.Collections.Generic;
using System.Linq;
using OpenCvSharp;

namespace MosaicCensorSystem.Detection
{
    /// <summary>
    /// SORT 알고리즘을 간소화하여 객체를 추적하는 클래스 (단순화 버전)
    /// </summary>
    public class SortTracker
    {
        private int nextId = 0;
        private readonly Dictionary<int, Track> activeTracks = new();
        private const float IouThreshold = 0.3f;
        private const int MaxAge = 10; // 프레임

        private class Track
        {
            public int Id;
            public Rect2d Box;
            public int Age;
        }

        /// <summary>
        /// 새로운 감지 결과를 기반으로 객체 추적을 업데이트합니다.
        /// </summary>
        public List<(int id, Rect2d box)> Update(List<Rect2d> detections)
        {
            // 1. 기존 트랙들의 나이를 1 증가
            foreach (var track in activeTracks.Values)
            {
                track.Age++;
            }

            // 2. 감지된 객체와 기존 트랙 매칭
            var matchedTracks = new HashSet<int>();
            var results = new List<(int id, Rect2d box)>();

            foreach (var det in detections)
            {
                int bestMatchId = -1;
                double maxIoU = IouThreshold;

                // 가장 IoU가 높은 트랙 찾기
                foreach (var track in activeTracks.Values)
                {
                    if (matchedTracks.Contains(track.Id)) continue;

                    double iou = ComputeIoU(track.Box, det);
                    if (iou > maxIoU)
                    {
                        maxIoU = iou;
                        bestMatchId = track.Id;
                    }
                }

                if (bestMatchId != -1)
                {
                    // 매칭 성공: 트랙 정보 업데이트
                    activeTracks[bestMatchId].Box = det;
                    activeTracks[bestMatchId].Age = 0; // 나이 초기화
                    matchedTracks.Add(bestMatchId);
                    results.Add((bestMatchId, det));
                }
                else
                {
                    // 매칭 실패: 새로운 트랙 생성
                    var newTrack = new Track { Id = nextId++, Box = det, Age = 0 };
                    activeTracks[newTrack.Id] = newTrack;
                    results.Add((newTrack.Id, newTrack.Box));
                }
            }

            // 3. 오래된 트랙 제거
            var oldTrackIds = activeTracks.Values
                .Where(t => t.Age > MaxAge)
                .Select(t => t.Id)
                .ToList();

            foreach (var id in oldTrackIds)
            {
                activeTracks.Remove(id);
            }

            return results;
        }

        /// <summary>
        /// 두 사각형의 IoU(Intersection over Union)를 계산합니다.
        /// </summary>
        private double ComputeIoU(Rect2d rectA, Rect2d rectB)
        {
            // 교차 영역 계산
            Rect2d intersection = rectA.Intersect(rectB);
            double intersectArea = intersection.Width * intersection.Height;

            // 합집합 영역 계산
            double unionArea = (rectA.Width * rectA.Height) + (rectB.Width * rectB.Height) - intersectArea;

            // IoU 반환
            return unionArea > 0 ? intersectArea / unionArea : 0;
        }
    }
}