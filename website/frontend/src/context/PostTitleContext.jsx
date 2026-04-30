import { createContext, useContext, useState } from 'react'

const PostTitleContext = createContext(null)

export function PostTitleProvider({ children }) {
  const [postTitle, setPostTitle] = useState(null)
  return (
    <PostTitleContext.Provider value={{ postTitle, setPostTitle }}>
      {children}
    </PostTitleContext.Provider>
  )
}

export function usePostTitle() {
  return useContext(PostTitleContext)
}
