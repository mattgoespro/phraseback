use crate::project::Result;
use serde::{Deserialize, Serialize};
use std::collections::BTreeSet;

#[derive(Clone)]
pub struct Descriptor {
    width: usize,
    height: usize,
    pixels: Vec<u8>,
}
impl Descriptor {
    pub fn decode(bytes: &[u8]) -> Result<Self> {
        if bytes.len() < 8 {
            return Err("Incomplete image descriptor".into());
        }
        let width = u32::from_le_bytes(bytes[..4].try_into()?) as usize;
        let height = u32::from_le_bytes(bytes[4..8].try_into()?) as usize;
        if width == 0
            || height == 0
            || width > 320
            || height > 320
            || bytes.len() != 8 + width * height * 3
        {
            return Err("Invalid image descriptor".into());
        }
        Ok(Self {
            width,
            height,
            pixels: bytes[8..].to_vec(),
        })
    }
    pub fn difference(&self, other: &Self) -> f64 {
        if self.width != other.width || self.height != other.height {
            return 100.0;
        }
        let mut max_tile = 0.0_f64;
        for y in (0..self.height).step_by(20) {
            for x in (0..self.width).step_by(20) {
                let (mut sum, mut count) = (0_u64, 0_u64);
                for row in y..(y + 20).min(self.height) {
                    for col in x..(x + 20).min(self.width) {
                        let i = (row * self.width + col) * 3;
                        for channel in 0..3 {
                            sum +=
                                self.pixels[i + channel].abs_diff(other.pixels[i + channel]) as u64;
                            count += 1;
                        }
                    }
                }
                max_tile = max_tile.max(sum as f64 / count as f64 / 4.0);
            }
        }
        let mut changed_edges = 0;
        for y in 0..self.height {
            for x in 1..self.width {
                let i = (y * self.width + x) * 3;
                let edge = |pixels: &[u8]| {
                    (0..3)
                        .map(|c| pixels[i + c] as i32 - pixels[i - 3 + c] as i32)
                        .sum::<i32>()
                };
                if (edge(&self.pixels) - edge(&other.pixels)).abs() > 54 {
                    changed_edges += 1;
                }
            }
        }
        max_tile.max(
            changed_edges as f64
                / (self.height * self.width.saturating_sub(1)).max(1) as f64
                / 0.004,
        )
    }
}
#[derive(Debug, Clone, Serialize, Deserialize, PartialEq)]
pub struct Suggestions {
    pub selected: Vec<usize>,
    pub intervals: Vec<(usize, usize)>,
}

/// Each run starts at a visually distinct state; brief states remain evidence.
pub fn from_runs(runs: &[(usize, usize, u64)], frames: usize) -> Suggestions {
    if frames == 0 {
        return Suggestions {
            selected: vec![],
            intervals: vec![],
        };
    }
    let mut selected = BTreeSet::from([0, frames - 1]);
    let mut intervals = Vec::new();
    let mut i = 0;
    while i < runs.len() {
        let mut j = i;
        while j < runs.len() && runs[j].2 < 375 {
            j += 1;
        }
        if j - i >= 3 && runs[i..i + 3].iter().map(|r| r.2).sum::<u64>() <= 750 {
            let first = runs[i].0;
            let last = runs[j.min(runs.len() - 1)].0;
            selected.extend([first, last]);
            intervals.push((first, last.max(runs[j - 1].1)));
            i = j.max(i + 1);
        } else {
            selected.insert(runs[i].0);
            i += 1;
        }
    }
    Suggestions {
        selected: selected.into_iter().collect(),
        intervals,
    }
}
#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn rapid_changes_group_but_brief_errors_remain() {
        let s = from_runs(&[(0, 0, 125), (1, 1, 125), (2, 2, 125), (3, 4, 800)], 5);
        assert_eq!(s.selected, vec![0, 3, 4]);
        assert_eq!(s.intervals, vec![(0, 3)]);
        assert_eq!(
            from_runs(&[(0, 0, 500), (1, 1, 125), (2, 2, 500)], 3).selected,
            vec![0, 1, 2]
        );
    }
    #[test]
    fn localized_changes_are_not_averaged_out() {
        let a = Descriptor {
            width: 320,
            height: 180,
            pixels: vec![0; 320 * 180 * 3],
        };
        let mut b = a.clone();
        for y in 0..20 {
            for x in 0..20 {
                b.pixels[(y * 320 + x) * 3] = 30;
            }
        }
        assert!(a.difference(&b) > 1.0);
        assert_eq!(a.difference(&a), 0.0);
    }
}
