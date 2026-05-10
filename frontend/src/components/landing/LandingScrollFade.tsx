'use client'

import { useEffect } from 'react'

export function LandingScrollFade() {
  useEffect(() => {
    const observer = new IntersectionObserver(
      (entries) => {
        entries.forEach((entry) => {
          if (entry.isIntersecting) {
            entry.target.classList.add('visible')
            observer.unobserve(entry.target)
          }
        })
      },
      { threshold: 0.15 },
    )

    const elements = document.querySelectorAll<HTMLElement>('.scroll-fade')
    elements.forEach((el, i) => {
      el.style.transitionDelay = `${i * 0.12}s`
      observer.observe(el)
    })

    return () => observer.disconnect()
  }, [])

  return null
}
