
#ifndef NMSEASHORE_MATHUTILS
#define NMSEASHORE_MATHUTILS

#define POW2(x) ((x)*(x))
#define SMOOTH(x) ((x)*(x)*(3.0-2.0*(x)))

// fiHashの改造品
// https://www.shadertoy.com/view/43jSRR

// The MIT License
// Copyright © 2024 Giorgi Azmaipharashvili
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions: The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software. THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
float hash(int2 p)
{
	uint2 u = p * int2(141421356, 2718281828);
	return float((u.x ^ u.y) * 3141592653u) * 0.00000000023283064365386963;	// float(~0u)の逆数を単精度に丸めた値
}

float noise1D(float x, int seed)
{
	int xi = floor(x);
	float t = SMOOTH(x - xi);
	
	uint2 q = uint2(xi + 0x7FFFFFFFU, seed + 0x7FFFFFFFU);
	
	float h0 = hash(q);
	q.x++;
	float h1 = hash(q);
	
	return lerp(h0, h1, t);
}

float fbm1d(float x, int seed, int level)
{
	float result = 0;
	float m = 0.5;
	
	for(int i = 0; i < level; i++)
	{
		result += noise1D(x / m, seed) * m;
		m *= 0.5;
	}
	
	return result * (1 / (1 - m * 2));
}

float absfbm1d(float x, int seed, int level)
{
	float result = 0;
	float m = 0.5;
	
	for(int i = 0; i < level; i++)
	{
		result += abs(noise1D(x / m, seed) - 0.5) * m;
		m *= 0.5;
	}
	
	// 値域は0-0.5
	return result * (1 / (1 - m * 2));
}

inline float bottomSmooth(float x)
{
	// smooth用パラメータ0.2固定での計算
	float a = x * x + 0.04;	// 0.2 * 0.2
	return (rsqrt(a) * a - 0.2) * 1.2198039293289185;	// sqrt(1 + 0.2 * 0.2) + 0.2
}

#endif
